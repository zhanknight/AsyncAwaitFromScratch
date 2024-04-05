using System.Collections.Concurrent;

AsyncLocal<int> asyncLocal = new();
for (int i = 0; i < 100; i++)
{
    asyncLocal.Value = i;

    MyThreadPool.QueueWorkItem(delegate
    {
        Console.WriteLine($"Task {asyncLocal.Value} has been grabbed and run by a thread!");
        Thread.Sleep(500);
    });
}
Console.ReadLine();

static class MyThreadPool
{
    // a blocking collection of actions/workitems (action is a delegate, a managed pointer to a method)
    // so we're storing a collection of methods to be executed
    // and the threads will be waiting to grab and run them
    // the collection is a tuple which also includes the execution context from when the item was added
    private static readonly BlockingCollection<(Action, ExecutionContext?)> workItems = new();

    // this method can be called to add one of those methods to the collection
    // so the threads can pick it up and run it
    // we also add the captured current execution context to bring along that async local variable
    public static void QueueWorkItem(Action action)
    {
        workItems.Add((action, ExecutionContext.Capture()));
    }

    // make a pool of threads in the constructor!
    static MyThreadPool()
    {
        // create five threads that will wait for work items to be added to the BlockingCollection
        // then grab and run - inside their original execution context - them when they appear
        for (int i = 0; i < 5; i++)
        {
            new Thread(() =>
            {
                while (true)
                {
                    Console.WriteLine("Thread is waiting for a task...");
                    (Action workItem, ExecutionContext? context) = workItems.Take();
                    Console.WriteLine("Thread is running a task...");
                        if (context == null)
                        {
                            workItem();
                        }
                        else
                        {
                            ExecutionContext.Run(context, delegate { workItem(); }, null);
                        }
                    Console.WriteLine("Thread completed a task!");


                }
            })
            {IsBackground = true }.Start();
        }
    }
}