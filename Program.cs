using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;



// demo the implementation
//
//
List<MyTask> taskList = new();
AsyncLocal<int> asyncLocal = new();

for (int i = 0; i < 100; i++)
{
    asyncLocal.Value = i;

    Thread.Sleep(Random.Shared.Next(50, 1000));

    taskList.Add(MyTask.Run(delegate
    {
        //Console.WriteLine($"Task {asyncLocal.Value} has been grabbed and run by a thread!");
        Thread.Sleep(Random.Shared.Next(1000, 4500));
    }));
}

foreach (var t in taskList) t.Wait();
// 
//
// demo the implementation



// Implement simple task class
class MyTask
{
    private bool _completed;
    private Exception? _exception;
    private Action? _continuation;
    private ExecutionContext? _context;

    public bool IsCompleted 
    { 
        get 
        { 
            lock (this)
            {
            return _completed;
            } 
        } 
    }
    public void SetResult() => CompleteIt(null);
    public void SetException(Exception e) => CompleteIt(e);
    private void CompleteIt(Exception? e)
    {
        lock(this)
        {
            if (_completed) throw new InvalidOperationException();

            _completed = true;
            _exception = e;

            if (_continuation != null)
            {
                if (_context == null)
                {
                    _continuation();
                }
                else
                {
                    ExecutionContext.Run(_context, delegate { _continuation(); }, null);
                }
            }   
        }
    }
    public void Wait() 
    {
        ManualResetEventSlim? mres = null;

        lock (this)
        {
            if (!_completed)
            {
                mres = new ManualResetEventSlim();
                ContinueWith(mres.Set); 
            }
        }
        mres?.Wait();

        if (_exception != null)
        {
            ExceptionDispatchInfo.Throw(_exception);
        }
    }
    public void ContinueWith(Action action) 
    {
        lock (this)
        {
            if (_completed)
            {
                MyThreadPool.QueueWorkItem(action);
            }
            else
            {
                _continuation = action;
                _context = ExecutionContext.Capture();  
            }
        }
    }

    public static MyTask Run(Action action)
    {
        MyTask mt = new MyTask();

        MyThreadPool.QueueWorkItem(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                mt.SetException(e);
                return;
            }
            mt.SetResult();
        });

        return mt;
    }

    // delay
    // whenall
    // whenany
    // iterate

}

// Implement a basic thread pool
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
        Console.WriteLine($"{workItems.Count}");
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
                    Console.WriteLine($"Thread {Thread.CurrentThread.Name} is waiting for a task...");
                    (Action workItem, ExecutionContext? context) = workItems.Take();
                    Console.WriteLine($"Thread {Thread.CurrentThread.Name} is running a task...");
                        if (context == null)
                        {
                            workItem();
                        }
                        else
                        {
                            ExecutionContext.Run(context, delegate { workItem(); }, null);
                        }
                    Console.WriteLine($"Thread {Thread.CurrentThread.Name} completed a task!");
                    Console.WriteLine($"{workItems.Count}");



                }
            })
            {IsBackground = true, Name = $"{i}" }.Start();
        }
    }
}