using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Networks.Policies;

namespace Stratis.Bitcoin.Networks
{
    public class LitecoinTest : LitecoinMain
    {
        public LitecoinTest()
        {
            this.Name = "LitecoinTest";
            this.AdditionalNames = new List<string> { "LtcTestnet" };

            this.RootFolderName = LitecoinRootFolderName;
            this.DefaultConfigFilename = LitecoinDefaultConfigFilename;
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            this.Magic = 0xf1c8d2fd;
            this.DefaultPort = 19335;
            this.DefaultMaxOutboundConnections = 8;
            this.DefaultMaxInboundConnections = 117;
            this.DefaultRPCPort = 19332;
            this.MaxTimeOffsetSeconds = LitecoinMaxTimeOffsetSeconds;
            this.MaxTipAge = LitecoinDefaultMaxTipAgeInSeconds;
            this.MinTxFee = 10000;
            this.FallbackFee = 20000;
            this.MinRelayTxFee = 1000;
            this.CoinTicker = "TLTC";

            var consensusFactory = LitecoinConsensusFactory.Instance;

            // Create the genesis block.
            this.GenesisTime = 1486949366;
            this.GenesisNonce = 293345;
            this.GenesisBits = 0x1e0ffff0;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            Block genesisBlock = CreateLitecoinGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            this.Genesis = genesisBlock;

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 76,
                [BuriedDeployments.BIP65] = 76,
                [BuriedDeployments.BIP66] = 76
            };

            var bip9Deployments = new BitcoinBIP9Deployments
            {
                // 1512 (75% for testchains)
                [BitcoinBIP9Deployments.TestDummy] = new BIP9DeploymentsParameters("TestDummy", 28, 1199145601, 1230767999, 1512),
                [BitcoinBIP9Deployments.CSV] = new BIP9DeploymentsParameters("CSV", 0, 1483228800, 1517356801, 1512),
                [BitcoinBIP9Deployments.Segwit] = new BIP9DeploymentsParameters("Segwit", 1, 1483228800, 1517356801, 1512)
            };

            this.Consensus = new NBitcoin.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: new ConsensusOptions(), // Default - set to Bitcoin params.
                coinType: 2,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 840000,
                majorityEnforceBlockUpgrade: 51,
                majorityRejectBlockOutdated: 75,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip34Hash: new uint256("8075c771ed8b495ffd943980a95f702ab34fce3c8c54e379548bda33cc8c0573"),
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 0,
                defaultAssumeValid: new uint256("0xfecb3e661f5facc5ac39e787c8d7d69138485206070e0712f3709758024c5241"), // 1025440
                maxMoney: long.MaxValue,
                coinbaseMaturity: 100,
                premineHeight: 0,
                premineReward: Money.Zero,
                proofOfWorkReward: Money.Coins(50),
                powTargetTimespan: TimeSpan.FromSeconds(3.5 * 24 * 60 * 60),
                powTargetSpacing: TimeSpan.FromSeconds(2.5 * 60),
                powAllowMinDifficultyBlocks: true,
                posNoRetargeting: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                minimumChainWork: new uint256("0x00000000000000000000000000000000000000000000000000203cafcb7de493"),
                isProofOfStake: false,
                lastPowBlock: default(int),
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero,
                bip9Deployments: bip9Deployments
            )
            {
                LitecoinWorkCalculation = true
            };

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (58) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { 0x04, 0x35, 0x87, 0xCF };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { 0x04, 0x35, 0x83, 0x94 };
            

            var encoder = new Bech32Encoder("tltc");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            // Partially obtained from https://github.com/litecoin-project/litecoin/blob/master/src/chainparams.cpp
            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 2056, new CheckpointInfo(new uint256("17748a31ba97afdc9a4f86837a39d287e3e7c7290a08a1d816c5969c78a83289"))}
            };

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("litecointools.com", "testnet-seed.litecointools.com"),
				new DNSSeedData("loshan.co.uk", "seed-b.litecoin.loshan.co.uk"),
				new DNSSeedData("thrasher.io", "dnsseed-testnet.thrasher.io")
            };

            //this.SeedNodes = new List<NetworkAddress> { new NetworkAddress(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9333)) };
            this.SeedNodes = ToSeed(pnSeed6_test).ToList();

            this.StandardScriptsRegistry = new BitcoinStandardScriptsRegistry();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x4966625a4b2851d9fdee139e56211a0d88575f59ed816ff5e6a63deb4e3e29a0"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x97ddfbbae6be97fd6cdf3e7ca13232a3afff2353e29badfab7f73011edd4ced9"));
        }

        static Tuple<byte[], int>[] pnSeed6_test = {
            Tuple.Create(new byte[]{0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0xff,0xff,0x68,0xec,0xd3,0xce}, 19335),
            Tuple.Create(new byte[]{0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0xff,0xff,0x42,0xb2,0xb6,0x23}, 19335)
        };

        public static string ConvertDeprecatedAddress(string address)
        {
            var data = Encoders.Base58.DecodeData(address);

            switch (data[0])
            {
                case 0xC4: // deprecated p2sh
                    data[0] = 0x3A; // new p2sh
                    break;
                case 0x05: // deprecated p2sh
                    data[0] = 0x32; // new p2sh
                    break;
            }

            return Encoders.Base58Check.EncodeData(data, 0, data.Length - 4);
        }
    }
}