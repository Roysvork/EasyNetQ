namespace AzureNetQ
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using AzureNetQ.Consumer;
    using AzureNetQ.FluentConfiguration;
    using AzureNetQ.Producer;

    using Microsoft.ServiceBus.Messaging;

    public class AzureBus : IBus
    {
        private readonly IAzureNetQLogger logger;

        private readonly IConventions conventions;

        private readonly IRpc rpc;

        private readonly ISendReceive sendReceive;

        private readonly IAzureAdvancedBus advancedBus;

        private readonly IConnectionConfiguration connectionConfiguration;

        private readonly ISerializer serializer;

        private readonly IExceptionHandler exceptionHandler;

        public AzureBus(
            IAzureNetQLogger logger,
            IConventions conventions,
            IRpc rpc,
            ISendReceive sendReceive,
            IAzureAdvancedBus advancedBus,
            IConnectionConfiguration connectionConfiguration,
            ISerializer serializer,
            IExceptionReporter exceptionReporter)
        {
            Preconditions.CheckNotNull(logger, "logger");
            Preconditions.CheckNotNull(conventions, "conventions");
            Preconditions.CheckNotNull(rpc, "rpc");
            Preconditions.CheckNotNull(sendReceive, "sendReceive");
            Preconditions.CheckNotNull(advancedBus, "advancedBus");
            Preconditions.CheckNotNull(connectionConfiguration, "connectionConfiguration");
            Preconditions.CheckNotNull(serializer, "serializer");
            Preconditions.CheckNotNull(exceptionReporter, "exceptionReporter");

            this.logger = logger;
            this.conventions = conventions;
            this.rpc = rpc;
            this.sendReceive = sendReceive;
            this.advancedBus = advancedBus;
            this.connectionConfiguration = connectionConfiguration;
            this.serializer = serializer;
            this.exceptionHandler = new ExceptionHandler(exceptionReporter);
        }

        public IAzureNetQLogger Logger
        {
            get { return this.logger; }
        }

        public IConventions Conventions
        {
            get { return this.conventions; }
        }

        public void Publish<T>(T message) where T : class
        {
            Preconditions.CheckNotNull(message, "message");

            this.PublishAsync(message).Wait();
        }

        public void Publish<T>(T message, string topic) where T : class
        {
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(topic, "topic");

            this.PublishAsync(message, topic).Wait();
        }

        public void Publish<T>(T message, Action<IPublishConfiguration> configure) where T : class
        {
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(configure, "configure");

            this.PublishAsync(message, configure).Wait();
        }

        public void Publish<T>(T message, string topic, Action<IPublishConfiguration> configure) where T : class
        {
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(topic, "topic");
            Preconditions.CheckNotNull(configure, "configure");

            this.PublishAsync(message, topic, configure).Wait();
        }

        public void Publish(Type type, object message)
        {
            Preconditions.CheckNotNull(type, "type");
            Preconditions.CheckNotNull(message, "message");

            this.PublishAsync(type, message, x => { }).Wait();
        }

        public void Publish(Type type, object message, string topic)
        {
            Preconditions.CheckNotNull(type, "type");
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(topic, "topic");

            this.PublishAsync(type, message, topic, x => { }).Wait();
        }

        public void Publish(Type type, object message, Action<IPublishConfiguration> configure)
        {
            Preconditions.CheckNotNull(type, "type");
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(configure, "configure");

            this.PublishAsync(type, message, configure).Wait();
        }

        public void Publish(Type type, object message, string topic, Action<IPublishConfiguration> configure)
        {
            Preconditions.CheckNotNull(type, "type");
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(topic, "topic");
            Preconditions.CheckNotNull(configure, "configure");

            this.PublishAsync(type, message, topic, configure).Wait();
        }

        public Task PublishAsync<T>(T message) where T : class
        {
            Preconditions.CheckNotNull(message, "message");

            return this.PublishAsync(message, x => { });
        }

        public Task PublishAsync<T>(T message, string topic) where T : class
        {
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(topic, "topic");

            return this.PublishAsync(message, topic, x => { });
        }

        public Task PublishAsync<T>(T message, Action<IPublishConfiguration> configure) where T : class
        {
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(configure, "configure");

            return this.PublishAsync(typeof(T), message, configure);
        }

        public Task PublishAsync<T>(T message, string topic, Action<IPublishConfiguration> configure) where T : class
        {
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(topic, "topic");
            Preconditions.CheckNotNull(configure, "configure");

            return this.PublishAsync(typeof(T), message, topic, configure);
        }

        public Task PublishAsync(Type type, object message)
        {
            Preconditions.CheckNotNull(type, "type");
            Preconditions.CheckNotNull(message, "message");

            return this.PublishAsync(type, message, x => { });
        }

        public Task PublishAsync(Type type, object message, string topic)
        {
            Preconditions.CheckNotNull(type, "type");
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(topic, "topic");

            return this.PublishAsync(type, message, topic, x => { });
        }

        public Task PublishAsync(Type type, object message, Action<IPublishConfiguration> configure)
        {
            Preconditions.CheckNotNull(type, "type");
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(configure, "configure");

            return this.PublishAsync(type, message, this.conventions.TopicNamingConvention(type), configure);
        }

        public Task PublishAsync(Type type, object message, string topicName, Action<IPublishConfiguration> configure)
        {
            Preconditions.CheckNotNull(message, "message");
            Preconditions.CheckNotNull(configure, "configure");

            var queueName = this.conventions.QueueNamingConvention(type);
            var queue = this.advancedBus.TopicFind(queueName);

            if (queue != null)
            {
                var configuration = new PublishConfiguration();
                configure(configuration);

                var content = this.serializer.MessageToString(message);
                var azureNetQMessage = new BrokeredMessage(content);
                azureNetQMessage.Properties.Add("topic", topicName);

                if (!string.IsNullOrEmpty(configuration.MessageId))
                {
                    azureNetQMessage.MessageId = configuration.MessageId;
                }

                this.InfoWrite(queueName, azureNetQMessage.MessageId, string.Format("Publishing message: {0}", content));
                return queue.SendAsync(azureNetQMessage);
            }

            var tcs = new TaskCompletionSource<bool>();
            tcs.SetResult(false);
            return tcs.Task;
        }

        public virtual void Subscribe<T>(Action<T> onMessage) where T : class
        {
            this.Subscribe(onMessage, x => { });
        }

        public virtual void Subscribe<T>(Action<T> onMessage, Action<ISubscriptionConfiguration> configure)
            where T : class
        {
            Preconditions.CheckNotNull(onMessage, "onMessage");
            Preconditions.CheckNotNull(configure, "configure");

            this.SubscribeAsync<T>(
                msg =>
                {
                    var tcs = new TaskCompletionSource<object>();
                    try
                    {
                        onMessage(msg);
                        tcs.SetResult(null);
                    }
                    catch (Exception exception)
                    {
                        tcs.SetException(exception);
                    }

                    return tcs.Task;
                },
                configure);
        }

        public virtual void SubscribeAsync<T>(Func<T, Task> onMessage) where T : class
        {
            this.SubscribeAsync(onMessage, x => { });
        }

        public virtual void SubscribeAsync<T>(Func<T, Task> onMessage, Action<ISubscriptionConfiguration> configure) where T : class
        {
            Preconditions.CheckNotNull(onMessage, "onMessage");
            Preconditions.CheckNotNull(configure, "configure");

            var configuration = new SubscriptionConfiguration();
            configure(configuration);

            var queueName = this.conventions.QueueNamingConvention(typeof(T));
            var subscriptionClient = this.advancedBus.SubscriptionDeclare(
                queueName,
                configuration.Topics,
                configuration.Subscription,
                configuration.ReceiveMode,
                configuration.RequiresDuplicateDetection,
                configuration.MaxDeliveryCount);

            var onMessageOptions = new OnMessageOptions
                                       {
                                           AutoComplete = false,
                                           MaxConcurrentCalls = this.connectionConfiguration.MaxConcurrentCalls
                                       };

            onMessageOptions.ExceptionReceived += this.exceptionHandler.ExceptionReceived;

            subscriptionClient.OnMessageAsync(
                message =>
                {
                    this.InfoWrite(queueName, message.MessageId, string.Format("Received message"));

                    try
                    {
                        var content = message.GetBody<string>();
                        var messageBody = serializer.StringToMessage<T>(content);

                        // Renew message lock every 30 seconds
                        var timer = new Timer(message.KeepLockAlive());
                        timer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

                        return onMessage(messageBody).ContinueWith(
                            o =>
                            {
                                timer.Dispose();

                                if (!o.IsFaulted && !o.IsCanceled)
                                {
                                    this.InfoWrite(
                                        queueName,
                                        message.MessageId,
                                        string.Format("Task completed succesfully"));

                                    return message.CompleteAsync();
                                }

                                if (o.IsFaulted && o.Exception != null)
                                {
                                    var ex = o.Exception.Flatten();
                                    var exceptionMessage = ex.InnerExceptions.First().Message;

                                    this.InfoWrite(
                                        queueName,
                                        message.MessageId,
                                        string.Format(
                                            "Task faulted on delivery attempt {1} - {2}",
                                            message.MessageId,
                                            message.DeliveryCount,
                                            exceptionMessage));
                                }
                                else
                                {
                                    this.InfoWrite(
                                        queueName,
                                        message.MessageId,
                                        string.Format(
                                            "Task was cancelled or no exception detail was available on delivery attempt {1}",
                                            message.MessageId,
                                            message.DeliveryCount));
                                }

                                return message.AbandonAsync();
                            });
                    }
                    catch (Exception ex)
                    {
                        return message.DeadLetterAsync();
                    }
                },
                onMessageOptions);
        }

        public TResponse Request<TRequest, TResponse>(TRequest request)
            where TRequest : class
            where TResponse : class
        {
            Preconditions.CheckNotNull(request, "request");

            var task = this.RequestAsync<TRequest, TResponse>(request);
            task.Wait();
            return task.Result;
        }

        public TResponse Request<TRequest, TResponse>(TRequest request, Action<IRequestConfiguration> configure)
            where TRequest : class
            where TResponse : class
        {
            Preconditions.CheckNotNull(request, "request");

            var task = this.RequestAsync<TRequest, TResponse>(request, configure);
            task.Wait();
            return task.Result;
        }

        public Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request)
            where TRequest : class
            where TResponse : class
        {
            Preconditions.CheckNotNull(request, "request");

            return this.rpc.Request<TRequest, TResponse>(request);
        }

        public Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request, Action<IRequestConfiguration> configure)
            where TRequest : class
            where TResponse : class
        {
            Preconditions.CheckNotNull(request, "request");

            return this.rpc.Request<TRequest, TResponse>(request, configure);
        }

        public virtual void Respond<TRequest, TResponse>(Func<TRequest, TResponse> responder)
            where TRequest : class
            where TResponse : class
        {
            this.Respond(responder, x => { });
        }

        public virtual void Respond<TRequest, TResponse>(Func<TRequest, TResponse> responder, Action<IRespondConfiguration> configure)
            where TRequest : class
            where TResponse : class
        {
            Preconditions.CheckNotNull(responder, "responder");

            Func<TRequest, Task<TResponse>> taskResponder =
                request => Task<TResponse>.Factory.StartNew(_ => responder(request), null);

            RespondAsync(taskResponder, configure);
        }

        public virtual void RespondAsync<TRequest, TResponse>(Func<TRequest, Task<TResponse>> responder)
            where TRequest : class
            where TResponse : class
        {
            this.RespondAsync(responder, x => { });
        }

        public virtual void RespondAsync<TRequest, TResponse>(Func<TRequest, Task<TResponse>> responder, Action<IRespondConfiguration> configure)
            where TRequest : class
            where TResponse : class
        {
            Preconditions.CheckNotNull(responder, "responder");

            this.rpc.Respond(responder, configure);
        }

        public void Send<T>(string queue, T message)
            where T : class
        {
            this.sendReceive.Send(queue, message);
        }

        public IDisposable Receive<T>(string queue, Action<T> onMessage)
            where T : class
        {
            return this.sendReceive.Receive(queue, onMessage);
        }

        public IDisposable Receive<T>(string queue, Func<T, Task> onMessage)
            where T : class
        {
            return this.sendReceive.Receive(queue, onMessage);
        }

        public IDisposable Receive(string queue, Action<IReceiveRegistration> addHandlers)
        {
            return this.sendReceive.Receive(queue, addHandlers);
        }

        public virtual void Dispose()
        {
            // throw new NotImplementedException();
        }

        private void InfoWrite(string queueName, string messageId, string logMessage)
        {
            this.logger.InfoWrite("{0} - {1}: {2}", queueName, messageId, logMessage);
        }
    }
}