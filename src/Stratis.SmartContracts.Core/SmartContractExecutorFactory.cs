﻿using NBitcoin;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Spawns SmartContractExecutor instances
    /// </summary>
    public class SmartContractExecutorFactory
    {
        private SmartContractDecompiler decompiler;
        private ISmartContractGasInjector gasInjector;
        private SmartContractValidator validator;
        private Network network;
        private IKeyEncodingStrategy keyEncodingStrategy;

        public SmartContractExecutorFactory(
            SmartContractDecompiler decompiler,
            ISmartContractGasInjector gasInjector,
            SmartContractValidator validator,
            IKeyEncodingStrategy keyEncodingStrategy,
            Network network)
        {
            this.decompiler = decompiler;
            this.gasInjector = gasInjector;
            this.validator = validator;
            this.network = network;
            this.keyEncodingStrategy = keyEncodingStrategy;
        }

        /// <summary>
        /// Initialize a smart contract executor for the block assembler or consensus validator. 
        /// <para>
        /// After the contract has been executed, it will process any fees and/or refunds.
        /// </para>
        /// </summary>
        public SmartContractExecutor CreateExecutor(
            SmartContractCarrier carrier,
            Money mempoolFee,
            IContractStateRepository stateRepository)
        {
            return SmartContractExecutor.Initialize(carrier, this.decompiler, this.gasInjector, this.network, stateRepository, this.validator, this.keyEncodingStrategy, mempoolFee);
        }
    }
}