using System.IO;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using DistributedJobScheduling.Logging;
using DistributedJobScheduling.DependencyInjection;
using DistributedJobScheduling.LifeCycle;
using System.Collections.Generic;
using DistributedJobScheduling.Serialization;

namespace DistributedJobScheduling.Communication.Basic.Speakers
{
    public class Speaker : IStartable
    {
        public bool IsConnected => _client != null && _client.Connected;
        public bool Running => _globalReceive;
        protected TcpClient _client;
        protected NetworkStream _stream;
        protected MemoryStream _memoryStream;
        protected byte[] _partialBuffer;
        protected int _lastTerminatorIndex;

        private CancellationTokenSource _sendToken;
        private CancellationTokenSource _receiveToken;
        private bool _globalReceive;
        private ISerializer _serializer;
        private SemaphoreSlim _sendSemaphore;

        protected Node _remote;

        public event Action<Node, Message> MessageReceived;
        public event Action<Node> Stopped;

        protected ILogger _logger;

        public Speaker(TcpClient client, Node remote, ISerializer serializer) : this(client, remote, serializer, DependencyManager.Get<ILogger>()) {}
        public Speaker(TcpClient client, Node remote, ISerializer serializer, ILogger logger)
        {
            _serializer = serializer;
            _logger = logger;
            _client = client;
            _remote = remote;
            _partialBuffer = new byte[4096];
            _memoryStream = new MemoryStream();
            _sendToken = new CancellationTokenSource();
            _receiveToken = new CancellationTokenSource();
            _sendSemaphore = new SemaphoreSlim(1,1);

            if(_client.Connected)
                _stream = _client.GetStream();
        }

        public void Stop()
        {
            if (_client != null)
            {
                _sendToken.Cancel();
                _receiveToken.Cancel();
                _globalReceive = false;
                _client.Close();
                _stream?.Close();
                _logger.Log(Tag.CommunicationBasic, $"Closed connection to {_remote}");
                Stopped?.Invoke(_remote);
                _client = null;
                _stream = null;
            }
        }

        private async Task<List<T>> Receive<T>() where T: Message
        {
            byte[] fullMessage = null;
            int bytesReceived = await _stream.ReadAsync(_partialBuffer, 0, _partialBuffer.Length, _receiveToken.Token);
            if(bytesReceived > 0)
            {
                //TODO: Restore LastIndexOf
                _lastTerminatorIndex = -1;
                for(int i = bytesReceived - 1; i >= 0; i--)
                    if(_partialBuffer[i] == '\0')
                    {
                        _lastTerminatorIndex = i;
                        break;
                    }
                
                _logger.Log(Tag.CommunicationBasic, $"Expecting {bytesReceived} bytes from {_remote} and terminator at {_lastTerminatorIndex}");
                if(_lastTerminatorIndex >= 0)
                {
                    //New Message Completed
                    await _memoryStream.WriteAsync(_partialBuffer, 0, _lastTerminatorIndex + 1, _receiveToken.Token);
                    fullMessage = _memoryStream.ToArray();
                    await _memoryStream.DisposeAsync();
                    _memoryStream = new MemoryStream();
                    await _memoryStream.WriteAsync(_partialBuffer, _lastTerminatorIndex + 1, bytesReceived - (_lastTerminatorIndex + 1), _receiveToken.Token);
                    _logger.Log(Tag.CommunicationBasic, $"Received {fullMessage.Length} bytes from {_remote}");
                    return ParseMessages<T>(fullMessage);
                }
                else
                {
                    //Partial message, continue
                    await _memoryStream.WriteAsync(_partialBuffer, 0, bytesReceived, _receiveToken.Token);
                    return null;
                }
            }
            else
            {
                _logger.Log(Tag.CommunicationBasic, $"Speaker closed with remote {_remote}");
                this.Stop();
                return null;
            }
        }

        private List<T> ParseMessages<T>(byte[] byteStream) 
            where T : Message
        {
            List<T> detectedMessages = new List<T>(1);
            int terminator = byteStream.Length;
            int lastTerminator = 0;
            do {
                terminator = Array.IndexOf(byteStream[lastTerminator..], (byte)'\0') + lastTerminator;
                if(terminator == -1)
                    _logger.Fatal(Tag.CommunicationBasic, $"No terminator in message {Encoding.UTF8.GetString(byteStream)}", new Exception("No terminator in message"));
                detectedMessages.Add(_serializer.Deserialize<T>(byteStream[lastTerminator..terminator]));
                lastTerminator = terminator + 1;
            } while(terminator < byteStream.Length - 1);
            return detectedMessages;
        }

        public async void Start()
        {
            _globalReceive = true;
            while(_globalReceive)
            {
                try
                {
                    List<Message> response = await Receive<Message>();
                    if(response != null)
                    {
                        _logger.Log(Tag.CommunicationBasic, $"Routing {response.Count} messages from {_remote.IP}");
                        response.ForEach(message => {
                            MessageReceived?.Invoke(_remote, message);
                        });
                    }
                }
                catch (ObjectDisposedException)
                {
                    _logger.Warning(Tag.CommunicationBasic, $"Failed receive from {_remote} because communication is closed");
                    break;
                }
                catch (Exception e)
                {
                    _logger.Error(Tag.CommunicationBasic, e);
                    break;
                }
            }
            _logger.Warning(Tag.CommunicationBasic, $"Stop receiving from {_remote}, stopped by cancellation");

            this.Stop();
        }

        public async Task Send(Message message)
        {
            try
            {
                if(IsConnected)
                {
                    byte[] bytes = _serializer.Serialize(message);

                    //One sender at a time
                    await _sendSemaphore.WaitAsync();
                    await _stream.WriteAsync(bytes, 0, bytes.Length, _sendToken.Token);
                    await _stream.WriteAsync(new byte[] { (byte)'\0' }, _sendToken.Token);
                    await _stream.FlushAsync(_sendToken.Token);
                    _sendSemaphore.Release();

                    _logger.Log(Tag.CommunicationBasic, $"Sent {bytes.Length} bytes to {_remote}");
                }
            }
            catch (ObjectDisposedException)
            {
                this.Stop();
                _logger.Warning(Tag.CommunicationBasic, $"Failed sent to {_remote} because communication is closed");
                return;
            }
            catch (Exception e)
            {
                this.Stop();
                _logger.Error(Tag.CommunicationBasic, $"Failed send to {_remote}", e);
            }
        }
    }
}