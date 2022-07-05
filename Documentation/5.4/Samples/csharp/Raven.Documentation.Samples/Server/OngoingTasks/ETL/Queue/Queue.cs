﻿using System;
using System.Collections.Generic;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.Backups;
using Raven.Client.Documents.Operations.ConnectionStrings;
using Raven.Client.Documents.Operations.ETL;
using Raven.Client.Documents.Operations.ETL.ElasticSearch;
using Raven.Client.Documents.Operations.ETL.OLAP;
using Raven.Client.Documents.Operations.ETL.Queue;
using Raven.Client.Documents.Operations.ETL.SQL;
namespace Raven.Documentation.Samples.Server.OngoingTasks.ETL.Queue
{
    public class ConnectionStrings
    {
        private interface IFoo
        {
            #region queue-broker-type
            public enum QueueBrokerType
            {
                None,
                Kafka,
                RabbitMq
            }
            #endregion

        }

        public ConnectionStrings()
        {
            using (var store = new DocumentStore())
            {
                #region add_rabbitMQ_connection-string
                var res = store.Maintenance.Send(new PutConnectionStringOperation<QueueConnectionString>(new QueueConnectionString
                {
                    Name = "RabbitMqConStr",
                    BrokerType = QueueBrokerType.RabbitMq,
                    RabbitMqConnectionSettings = new RabbitMqConnectionSettings() { ConnectionString = "amqp://guest:guest@localhost:5672/" }
                }));
                #endregion
            }

            using (var store = new DocumentStore())
            {
                #region add_kafka_connection-string
                var res = store.Maintenance.Send(new PutConnectionStringOperation<QueueConnectionString>(new QueueConnectionString
                {
                    Name = "KafkaConStr",
                    BrokerType = QueueBrokerType.Kafka,
                    KafkaConnectionSettings = new KafkaConnectionSettings() { BootstrapServers = "localhost:29092" }
                }));
                #endregion
            }
        }

        // Add Kafka ETL Task
        public void AddKafkaEtlTask()
        {
            using (var store = new DocumentStore())
            {
                // Create a document
                using (var session = store.OpenSession())
                {
                    #region add_kafka_etl-task
                    // use PutConnectionStringOperation to add connection string
                    var res = store.Maintenance.Send(
                        new PutConnectionStringOperation<QueueConnectionString>(new QueueConnectionString
                        {
                            Name = "KafkaConStr",
                            BrokerType = QueueBrokerType.Kafka,
                            KafkaConnectionSettings = new KafkaConnectionSettings() { BootstrapServers = "localhost:9092" }
                        }));

                    // create transformation script
                    Transformation transformation = new Transformation
                    {
                        Name = "scriptName",
                        Collections = { "Orders" },
                        Script = @"var orderData = {
    Id: id(this), // property with RavenDB document ID
    OrderLinesCount: this.Lines.length,
    TotalCost: 0
};

for (var i = 0; i < this.Lines.length; i++) {
    var line = this.Lines[i];
    var cost = (line.Quantity * line.PricePerUnit) * ( 1 - line.Discount);
    orderData.TotalCost += cost;
}

loadToOrders(orderData, {  // load to the 'Orders' Topic with optional params
    Id: id(this),
    PartitionKey: id(this),
    Type: 'com.github.users',
    Source: '/registrations/direct-signup'
});",
                        ApplyToAllDocuments = false
                    };

                    // use AddEtlOperation to add ETL task 
                    AddEtlOperation<QueueConnectionString> operation = new AddEtlOperation<QueueConnectionString>(
                    new QueueEtlConfiguration()
                    {
                        Name = "KafkaEtlTaskName",
                        ConnectionStringName = "KafkaConStr",
                        Transforms =
                            {
                                transformation
                            },
                        Queues = { new EtlQueue() { Name = "Orders" } },
                        BrokerType = QueueBrokerType.Kafka
                    });
                    store.Maintenance.Send(operation);
                    
                    #endregion
                }
            }
        }

        // Add RabbitMq ETL task
        public void AddRabbitmqEtlTask()
        {
            using (var store = new DocumentStore())
            {
                // Create a document
                using (var session = store.OpenSession())
                {
                    #region add_rabbitmq_etl-task
                    // use PutConnectionStringOperation to add connection string
                    var res = store.Maintenance.Send(
                        new PutConnectionStringOperation<QueueConnectionString>(new QueueConnectionString
                        {
                            Name = "RabbitMqConStr",
                            BrokerType = QueueBrokerType.RabbitMq,
                            RabbitMqConnectionSettings = new RabbitMqConnectionSettings() { ConnectionString = "amqp://guest:guest@localhost:5672/" }
                        }));

                    // create transformation script
                    Transformation transformation = new Transformation
                    {
                        Name = "scriptName",
                        Collections = { "Orders" },
                        Script = @"var orderData = {
    Id: id(this), 
    OrderLinesCount: this.Lines.length,
    TotalCost: 0
};

for (var i = 0; i < this.Lines.length; i++) {
    var line = this.Lines[i];
    var cost = (line.Quantity * line.PricePerUnit) * ( 1 - line.Discount);
    orderData.TotalCost += cost;
}

loadToOrders(orderData, `routingKey`, {  
    Id: id(this),
    PartitionKey: id(this),
    Type: 'com.github.users',
    Source: '/registrations/direct-signup'
});",
                        ApplyToAllDocuments = false
                    };

                    // use AddEtlOperation to add ETL task 
                    AddEtlOperation<QueueConnectionString> operation = new AddEtlOperation<QueueConnectionString>(
                    new QueueEtlConfiguration()
                    {
                        Name = "RabbitMqEtlTaskName",
                        ConnectionStringName = "RabbitMqConStr",
                        Transforms =
                            {
                                transformation
                            },
                        Queues = { new EtlQueue() { Name = "Orders" } },
                        BrokerType = QueueBrokerType.RabbitMq,
                        SkipAutomaticQueueDeclaration = false
                    });
                    store.Maintenance.Send(operation);

                    #endregion
                }
            }
        }
    }
}
