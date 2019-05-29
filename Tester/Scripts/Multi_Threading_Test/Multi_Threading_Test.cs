using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class Multi_Threading_Test
{
	// Test speed of different asynchronous call types.
	public static void ProcessArguments(string[] args)
	{
		var tests = 25;
		var calls = 2000;
		Console.WriteLine("{1} tests x {0} parallel calls each.\r\n", calls, tests);
		var values = Enum.GetValues(typeof(TestType));
		var watch = new Stopwatch();
		foreach (TestType value in values)
		{
			Console.Write("Running {0, -11} : ", value);
			var elapsed = 0.0;
			for (var j = 0; j < tests; j++)
			{
				Write(j, tests);
				watch.Restart();
				// Lock, which will used to wait for all jobs to finish.
				var resetEvent = new ManualResetEvent(false);
				var callsLeft = calls;
				var action = (Action<object>)delegate (object state)
				{
					try { ToDoJob(); } catch { }
					// Safely decrement the counter and unlock when all jobs are finished.
					if (Interlocked.Decrement(ref callsLeft) == 0)
						resetEvent.Set();
				};
				for (var i = 0; i < calls; i++)
					switch (value)
					{
						case TestType.Task: Task.Factory.StartNew(action, null); break;
						case TestType.LongTask: Task.Factory.StartNew(action, null, TaskCreationOptions.LongRunning); break;
						case TestType.ThreadPool: ThreadPool.QueueUserWorkItem(new WaitCallback(action), null); break;
						case TestType.BeginInvoke: action.BeginInvoke(null, null, null); break;
						case TestType.Thread: new Thread(new ParameterizedThreadStart(action)).Start(); break;
					}
				// Lock until 'Set' is called.
				resetEvent.WaitOne();
				watch.Stop();
				elapsed += watch.ElapsedMilliseconds;
			}
			Console.WriteLine(" - done {0:# ##0} calls in {1,6:0.00}ms average", calls, elapsed / tests);
		}
	}

	enum TestType { Task, ThreadPool, BeginInvoke, Thread, LongTask }

	/// <summary>
	/// The do stuff.
	/// </summary>
	public static void ToDoJob()
	{
		//Console.Write("*");
	}

	public static void Write(int i, int max)
	{
		var l = max.ToString().Length;
		var s = string.Format("{0," + l + "}/{1}", i + 1, max);
		Console.CursorVisible = i + 1 == max;
		if (i > 0)
			for (var c = 0; c < s.Length; c++)
				Console.Write("\b");
		Console.Write(s);
	}

}

