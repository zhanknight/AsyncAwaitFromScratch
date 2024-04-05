using System.Collections.Concurrent;


for (int i = 0; i < 100; i++)
{
    int counter = i;

    MyThreadPool.QueueWorkItem(delegate
    {
        Console.WriteLine($"Task {counter} has been grabbed and run by a thread!");
        Thread.Sleep(500);
    });
}
Console.ReadLine();

static class MyThreadPool
{
    // a blocking collection of actions/workitems (action is a delegate, a managed pointer to a method)
    // so we're storing a collection of methods to be executed
    // and the threads will be waiting to grab and run them
    private static readonly BlockingCollection<Action> workItems = new();

    // this method can be called to add one of those methods to the collection so the threads can pick it up and run it
    public static void QueueWorkItem(Action action)
    {
        workItems.Add(action);
    }

    // make a pool of threads in the constructor!
    static MyThreadPool()
    {
        // create five threads that will wait for work items to be added to the BlockingCollection
        // then grab and run them when they appear
        for (int i = 0; i < 5; i++)
        {
            new Thread(() =>
            {
                while (true)
                {
                    Console.WriteLine("Thread is waiting for a task...");
                    Action workItem = workItems.Take();
                    Console.WriteLine("Thread is running a task...");
                    workItem();
                    Console.WriteLine("Thread completed a task!");
                }
            })
            {IsBackground = true }.Start();
        }
    }
}