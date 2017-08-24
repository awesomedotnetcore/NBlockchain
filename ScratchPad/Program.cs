﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBlockchain.Interfaces;
using NBlockchain.Models;
using NBlockchain.Services.Hashers;
using NBlockchain.Services.PeerDiscovery;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ScratchPad
{
    class Program
    {
        static void Main(string[] args)
        {
            IServiceProvider miner1 = ConfigureNode("miner1", 500, "", true);
            //IServiceProvider miner2 = ConfigureNode("miner2", 501, "tcp://localhost:500", true);
            IServiceProvider node1;// = ConfigureNode("node1", true);

            //Console.WriteLine("starting miner");
            var keys1 = RunMiner(miner1, true);

            //var miner1Net = miner1.GetService<IPeerNetwork>();

            //RunMiner(miner2, false);

            //Task.Factory.StartNew(async () =>
            //{
            //    await Task.Delay(5000);
            //    Console.WriteLine("starting node");
            //    node1 = ConfigureNode("node1", 502, "tcp://localhost:500", true);
            //    await RunNode(node1, true);
            //});


            //RunNode(node1, true);
            //var block = blockBuilder.BuildBlock(new byte[0], minerKeys).Result;

            //blockValidator.ConfirmBlock(block).Wait();

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await Task.Delay(5000);
                    Console.WriteLine("checking balances...");
                    try
                    {
                        Console.WriteLine($"miner1 balance: {GetBalance(miner1, keys1)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            });

            Console.ReadLine();
        }

        private static KeyPair RunMiner(IServiceProvider sp, bool genesis)
        {
            var node = sp.GetService<INodeHost>();
            var network = sp.GetService<IPeerNetwork>();
            var sigService = sp.GetService<ISignatureService>();
            var addressEncoder = sp.GetService<IAddressEncoder>();

            network.Open();

            //var minerKeys = sigService.GenerateKeyPair();

            var keys = sigService.GenerateKeyPair();
            //var keys2 = sigService.GenerateKeyPair();
            var address = addressEncoder.EncodeAddress(keys.PublicKey, 0);

            if (genesis)
                node.BuildGenesisBlock(keys).Wait();

            node.StartBuildingBlocks(keys);

            return keys;            
        }

        private static void SendTxn(IServiceProvider sp, KeyPair keys, string address, decimal amount)
        {
            var node = sp.GetService<INodeHost>();            
            var sigService = sp.GetService<ISignatureService>();
            var addressEncoder = sp.GetService<IAddressEncoder>();
            var origin = addressEncoder.EncodeAddress(keys.PublicKey, 0);

            var txn1 = new TestTransaction()
            {
                Message = "hello",
                Amount = amount,
                Destination = address
            };

            var txn1env = new TransactionEnvelope(txn1)
            {
                OriginKey = Guid.NewGuid(),
                TransactionType = "test-v1",
                Originator = origin
            };

            sigService.SignTransaction(txn1env, keys.PrivateKey);

            node.SendTransaction(txn1env);
        }

        private static decimal GetBalance(IServiceProvider sp, KeyPair keys)
        {
            var repo = sp.GetService<ITransactionRepository>();            
            var addressEncoder = sp.GetService<IAddressEncoder>();
            var address = addressEncoder.EncodeAddress(keys.PublicKey, 0);

            return repo.GetAccountBalance(address);
        }

        private static KeyPair RunNode(IServiceProvider sp)
        {
            var node = sp.GetService<INodeHost>();
            var network = sp.GetService<IPeerNetwork>();
            var sigService = sp.GetService<ISignatureService>();
            var addressEncoder = sp.GetService<IAddressEncoder>();
            var keys = sigService.GenerateKeyPair();
            network.Open();

            //var address = addressEncoder.EncodeAddress(keys.PublicKey, 0);
            return keys;
        }

        private static IServiceProvider ConfigureNode(string db, uint port, string peerStr, bool logging)
        {
            //setup dependency injection
            IServiceCollection services = new ServiceCollection();
            services.AddBlockchain(x =>
            {
                x.UseMongoDB(@"mongodb://localhost:27017", db)
                    .UseTransactionRepository<ITransactionRepository, TransactionRepository>();
                x.UseTcpPeerNetwork(port);
                x.AddPeerDiscovery(sp => new StaticPeerDiscovery(peerStr));
                //x.UseMulticastDiscovery("test", "224.100.0.1", 8088);
                x.AddTransactionType<TestTransaction>();
                x.AddTransactionType<CoinbaseTransaction>();
                x.AddValidator<TestTransactionValidator>();
                x.AddValidator<CoinbaseTransactionValidator>();
                x.UseBlockbaseProvider<TestBlockbaseBuilder>();
                x.UseParameters(new StaticNetworkParameters()
                {
                    BlockTime = TimeSpan.FromSeconds(10),
                    Difficulty = 200,
                    HeaderVersion = 1,
                    ExpectedContentThreshold = 0.8m
                });
            });

            services.AddLogging();            
            var serviceProvider = services.BuildServiceProvider();

            //config logging
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            if (logging)
            {
                loggerFactory.AddDebug();
                loggerFactory.AddConsole(LogLevel.Debug);
            }

            return serviceProvider;
        }
    }
}