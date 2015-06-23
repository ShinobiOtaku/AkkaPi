using Akka.Actor;
using Akka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pi
{
   class Program
   {
      static void Main(string[] args)
      {
         var system = ActorSystem.Create("Pi");
         var main = system.ActorOf<Coordinator>();

         main.Tell(new RunOptions { Length = 800000000, NumberOfWorkers = 8 });
         Console.ReadLine();
      }
   }

   class RunOptions
   {
      public int Length { get; set; }
      public short NumberOfWorkers { get; set; }
   }

   class Coordinator : ReceiveActor
   {
      IActorRef _acc;
      IActorRef _workers;

      public Coordinator()
      {
         Receive<RunOptions>(options =>
         {
            var jobs = YieldEquallySplitJobs(options.NumberOfWorkers, options.Length).ToList();

            _acc = Context.ActorOf(
               Props.Create(() => new Accumulator(jobs.Count)), "accumulator");

            _workers = Context.ActorOf(
               Props.Create(() => new Worker(_acc)).WithRouter(new RoundRobinPool(options.NumberOfWorkers)), "workers");

            foreach (var job in jobs)
               _workers.Tell(job);
         });
      }

      IEnumerable<Worker.Job> YieldEquallySplitJobs(int numberOfWorkers, int length)
      {
         var batchSize = length / numberOfWorkers;

         int i = 0;

         while(i < numberOfWorkers - 1)
         {
            yield return new Worker.Job { Start = i * batchSize, Length = batchSize };
            i++;
         }

         var next = (i * batchSize);
         var leftOver = length - next;
         yield return new Worker.Job { Start = next, Length = leftOver };
      }
   }

   class Worker : ReceiveActor
   {
      public class Job
      {
         public int Start { get; set; }
         public int Length { get; set; }
      }

      public Worker(IActorRef accumulator)
      {
         Receive<Job>(range =>
         {
            var result = Enumerable
               .Range(range.Start, range.Length)
               .Sum(num => 4 * (Math.Pow(-1, num) / (2 * num + 1)));

            accumulator.Tell(result);
         });
      }
   }

   class Accumulator : ReceiveActor
   {
      int _receivedMessages;
      int _expectedMessages;
      double _pi;
      DateTime _startTime;

      public Accumulator(int iterations)
      {
         _expectedMessages = iterations;

         Receive<double>(result =>
         {
            _pi += result;
            _receivedMessages += 1;

            if (_receivedMessages == _expectedMessages)
               Self.Tell(PoisonPill.Instance);
         });
      }

      protected override void PreStart()
      {
         _startTime = DateTime.Now;
         base.PreStart();
      }

      protected override void PostStop()
      {
         Console.WriteLine("Pi: {0}, in {1}s", _pi, (DateTime.Now - _startTime).TotalSeconds);
         base.PostStop();
      }
   }
}
