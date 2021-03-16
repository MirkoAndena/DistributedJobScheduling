# Distributed Systems Project
## Distributed Job Scheduling
### Assumptions
- The application starts with at least one executor already knowing he's the leader and doesn't crash until at least another node joined the view
- Links are reliable
- Processes may fail but they resume back and start the work again (resume from stable storage)
- Jobs are abstracted as wait operations of different length which can lockup the network thread and other components randomly
- Executor nodes are already know to each executor (group.json)
- No form of authentication for executors
- Nodes do not crash in the middle of the join procedure