﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using Rebus.Extensions;
using Rebus.Messages;

namespace Rebus.Transport.InMem
{
    /// <summary>
    /// Defines a network that the in-mem transport can work on, functioning as a namespace for the queue addresses
    /// </summary>
    public class InMemNetwork
    {
        static int _networkIdCounter;

        readonly string _networkId = string.Format("In-mem network {0}", Interlocked.Increment(ref _networkIdCounter));

        readonly ConcurrentDictionary<string, ConcurrentQueue<InMemTransportMessage>> _queues =
            new ConcurrentDictionary<string, ConcurrentQueue<InMemTransportMessage>>(StringComparer.InvariantCultureIgnoreCase);

        readonly bool _outputEventsToConsole;

        /// <summary>
        /// Constructs the in-mem network, optionally (if <see cref="outputEventsToConsole"/> is set to true) outputting information
        /// about what is happening inside it to <see cref="Console.Out"/>
        /// </summary>
        public InMemNetwork(bool outputEventsToConsole = false)
        {
            _outputEventsToConsole = outputEventsToConsole;

            if (_outputEventsToConsole)
            {
                Console.WriteLine("Created in-mem network '{0}'", _networkId);
            }
        }

        /// <summary>
        /// Resets the network (i.e. all queues and their messages are deleted)
        /// </summary>
        public void Reset()
        {
            Console.WriteLine("Resetting in-mem network '{0}'", _networkId);
            _queues.Clear();
        }

        /// <summary>
        /// Delivers the specified <see cref="InMemTransportMessage"/> to the address specified by <see cref="destinationAddress"/>.
        /// If <see cref="alwaysQuiet"/> is set to true, no events will ever be printed to <see cref="Console.Out"/>
        /// (can be used by an in-mem transport to return a message to a queue, as if there was a queue transaction that was rolled back)
        /// </summary>
        public void Deliver(string destinationAddress, InMemTransportMessage msg, bool alwaysQuiet = false)
        {
            if (destinationAddress == null) throw new ArgumentNullException("destinationAddress");
            if (msg == null) throw new ArgumentNullException("msg");

            if (_outputEventsToConsole && !alwaysQuiet)
            {
                Console.WriteLine("{0} ---> {1} ({2})", msg.Headers.GetValueOrNull(Headers.MessageId) ?? "<no message ID>", destinationAddress, _networkId);
            }

            var messageQueue = _queues
                .GetOrAdd(destinationAddress, address => new ConcurrentQueue<InMemTransportMessage>());

            messageQueue.Enqueue(msg);
        }

        /// <summary>
        /// Gets the next message from the queue with the given <see cref="inputQueueName"/>, returning null if no messages are available.
        /// </summary>
        public InMemTransportMessage GetNextOrNull(string inputQueueName)
        {
            if (inputQueueName == null) throw new ArgumentNullException("inputQueueName");

            InMemTransportMessage message;

            var messageQueue = _queues.GetOrAdd(inputQueueName, address => new ConcurrentQueue<InMemTransportMessage>());

            if (!messageQueue.TryDequeue(out message)) return null;

            if (MessageIsExpired(message))
            {
                Console.WriteLine("{0} EXP> {1} ({2})", inputQueueName, message.Headers.GetValueOrNull(Headers.MessageId) ?? "<no message ID>", _networkId);
                return null;
            }

            if (_outputEventsToConsole)
            {
                Console.WriteLine("{0} ---> {1} ({2})", inputQueueName, message.Headers.GetValueOrNull(Headers.MessageId) ?? "<no message ID>", _networkId);
            }

            return message;
        }

        bool MessageIsExpired(InMemTransportMessage message)
        {
            var headers= message.Headers;
            if (!headers.ContainsKey(Headers.TimeToBeReceived)) return false;

            var timeToBeReceived = headers[Headers.TimeToBeReceived];
            var maximumAge = TimeSpan.Parse(timeToBeReceived);

            return message.Age > maximumAge;
        }

        /// <summary>
        /// Returns whether the network has a queue with the specified name
        /// </summary>
        public bool HasQueue(string address)
        {
            return _queues.ContainsKey(address);
        }

        /// <summary>
        /// Creates a queue on the network with the specified name
        /// </summary>
        public void CreateQueue(string address)
        {
            _queues.TryAdd(address, new ConcurrentQueue<InMemTransportMessage>());
        }
    }
}