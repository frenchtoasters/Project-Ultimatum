using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

//Note: This is based on Switcheo exchange smart contract.

namespace splashpool
{
	public class SplashPoolContract : SmartContract
	{
		public delegate object NEP5Contract(string method, object[] args);

		//Events
		[DisplayName("createPool")]
		public static event Action<byte[], byte[], BigInteger, BigInteger, BigInteger, BigInteger> PoolCreated; // (operator_key, operator_msg, size, min, max, state)

		[DisplayName("depositPool")]
		public static event Action<byte[], byte[], BigInteger> Deposited;// (pool_id, assetID, amount)

	        [DisplayName("failed")]
        	public static event Action<byte[], byte[]> Failed; // (address, offerHash)

       		[DisplayName("cancelled")]
        	public static event Action<byte[], byte[]> Cancelled; // (address, offerHash)

	        [DisplayName("transferred")]
        	public static event Action<byte[], byte[], BigInteger> Transferred; // (address, assetID, amount)

        	[DisplayName("withdrawing")]
        	public static event Action<byte[], byte[], BigInteger> Withdrawing; // (address, assetID, amount)

        	[DisplayName("withdrawn")]
        	public static event Action<byte[], byte[], BigInteger> Withdrawn; // (address, assetID, amount)

		//Splash Pool Settings & Hardcaps
		private static readonly byte[] Owner = "".ToScriptHash(); //Should be set to wallet owner
		//private static readonly byte[] NativeToke = "".ToScriptHash(); //Should be set to script hash of token if created
		private const ulong feeFactor = 1000000; // 1 => 0.0001%
		private const int maxFee = 5000; // 5000/1000000 = 0.5%
		private const int bucketDuration = 82800; // 82800secs = 23hrs
		private const int nativeTokenDiscount = 2; // 1/2 => 50%

		//Contract States
		private static readonly byte[] Pending = { }; //only can initialize
		private static readonly byte[] Active = { 0x01 }; // all operations active
		private static readonly byte[] Inactive = { 0x02 }; // deposits halted - only can cancel, withdraw & owner actions

		//Asset Categories
		private static readonly byte[] SystemAsset = { 0x99 };
		private static readonly byte[] NEP5 = { 0x98 };

		//Withdraw Flags
        	private static readonly byte[] Mark = { 0x50 };
        	private static readonly byte[] Withdraw = { 0x51 };
        	private static readonly byte[] OpCode_TailCall = { 0x69 };
        	private static readonly byte Type_InvocationTransaction = 0xd1;
        	private static readonly byte TAUsage_WithdrawalStage = 0xa1;
        	private static readonly byte TAUsage_NEP5AssetID = 0xa2;
        	private static readonly byte TAUsage_SystemAssetID = 0xa3;
        	private static readonly byte TAUsage_WithdrawalAddress = 0xa4;
        	private static readonly byte TAUsage_AdditionalWitness = 0x20; // additional verification script which can be used to ensure any withdrawal txns are intended by the owner

		//Byte Constants
		private static readonly byte[] Empty = { };
        	private static readonly byte[] Zeroes = { 0, 0, 0, 0, 0, 0, 0, 0 }; // for fixed8 (8 bytes)
        	private static readonly byte[] Null = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // for fixed width list ptr (32bytes)        
		
		private struct Pool
		{
			public byte[] operator_key;
			public byte[] operator_msg;
			public byte[] pool_id;
			public byte[] deposit_ids; //key to storage array
			public BigInteger size;
			public BigInteger min;
			public BigInteger max;
			public BigInteger current;
			public Bool result;
		}
		
		private struct Deposit
		{
			public byte[] pool_id; 
			public byte[] asset_id;
			public byte[] amount;
		}

		private struct Receipt
		{
			public byte[] deposit_id;
			public Bool result;
		}
		/*
		 * Will probably need something like this later
		private static Offer NewOffer(
            		byte[] makerAddress,
            		byte[] offerAssetID, byte[] offerAmount,
            		byte[] wantAssetID, byte[] wantAmount,
            		byte[] availableAmount,
            		byte[] nonce
        	)
        	{
            		var offerAssetCategory = NEP5;
            		var wantAssetCategory = NEP5;
            		if (offerAssetID.Length == 32) offerAssetCategory = SystemAsset;
            		if (wantAssetID.Length == 32) wantAssetCategory = SystemAsset;

            		return new Offer
            		{
                		MakerAddress = makerAddress.Take(20),
                		OfferAssetID = offerAssetID,
                		OfferAssetCategory = offerAssetCategory,
                		OfferAmount = offerAmount.AsBigInteger(),
                		WantAssetID = wantAssetID,
                		WantAssetCategory = wantAssetCategory,
                		WantAmount = wantAmount.AsBigInteger(),
                		AvailableAmount = availableAmount.AsBigInteger(),
                		Nonce = nonce,
            		};
        	}
		*/
	        public static object Main(string operation, params object[] args)
        	{
            	if (Runtime.Trigger == TriggerType.Verification)
            	{
            	    if (GetState() == Pending) return false;

                	var currentTxn = (Transaction)ExecutionEngine.ScriptContainer;
                	var withdrawalStage = WithdrawalStage(currentTxn);
                	var withdrawingAddr = GetWithdrawalAddress(currentTxn, withdrawalStage);
                	var assetID = GetWithdrawalAsset(currentTxn);
                	var isWithdrawingNEP5 = assetID.Length == 20;
                	var inputs = currentTxn.GetInputs();
                	var outputs = currentTxn.GetOutputs();

                	ulong totalOut = 0;
                	if (withdrawalStage == Mark)
                	{
                    		// Check that txn is signed
                    		if (!Runtime.CheckWitness(withdrawingAddr)) return false;

                    		// Check that withdrawal is possible
                    		if (!VerifyWithdrawal(withdrawingAddr, assetID)) return false;

                    		// Check that inputs are not already reserved
                    		foreach (var i in inputs)
                    		{
                    		    if (Storage.Get(Context(), i.PrevHash.Concat(IndexAsByteArray(i.PrevIndex))).Length > 0) return false;
                    		}

                    		// Check that outputs are a valid self-send
                    		var authorizedAssetID = isWithdrawingNEP5 ? GasAssetID : assetID;
                    		foreach (var o in outputs)
                    		{
                    		    totalOut += (ulong)o.Value;
                    		    if (o.ScriptHash != ExecutionEngine.ExecutingScriptHash) return false;
                    		    if (o.AssetId != authorizedAssetID) return false;
                    		}

                    		// Check that NEP5 withdrawals don't reserve more utxos than required
                    		if (isWithdrawingNEP5)
                    		{
                    		    if (inputs.Length > 1) return false;
                    		    if (outputs[0].Value > 1) return false;
                    		}

                    		// Check that inputs are not wasted (prevent DOS on withdrawals)
                    		if (outputs.Length - inputs.Length > 1) return false;
                	}
                	else if (withdrawalStage == Withdraw)
                	{
                    		// Check that utxo has been reserved
                    		foreach (var i in inputs)
                    		{
                    		    if (Storage.Get(Context(), i.PrevHash.Concat(IndexAsByteArray(i.PrevIndex))) != withdrawingAddr) return false;
                    		}

                    		// Check withdrawal destinations
                    		var authorizedAssetID = isWithdrawingNEP5 ? GasAssetID : assetID;
                    		var authorizedAddress = isWithdrawingNEP5 ? ExecutionEngine.ExecutingScriptHash : withdrawingAddr;
                    		foreach (var o in outputs)
                    		{
                    		    totalOut += (ulong)o.Value;
                    		    if (o.AssetId != authorizedAssetID) return false;
                    		    if (o.ScriptHash != authorizedAddress) return false;
                    		}

                   		 // Check withdrawal amount
                    		var authorizedAmount = isWithdrawingNEP5 ? 1 : GetWithdrawAmount(withdrawingAddr, assetID);
                    		if (totalOut != authorizedAmount) return false;
                	}
                	else
                	{
                	    return false;
                	}

                	// Ensure that nothing is burnt
                	ulong totalIn = 0;
                	foreach (var i in currentTxn.GetReferences()) totalIn += (ulong)i.Value;
                	if (totalIn != totalOut) return false;

                	// Check that Application trigger will be tail called with the correct params
                	if (currentTxn.Type != Type_InvocationTransaction) return false;
                	var invocationTransaction = (InvocationTransaction)currentTxn;
                	if (invocationTransaction.Script != WithdrawArgs.Concat(OpCode_TailCall).Concat(ExecutionEngine.ExecutingScriptHash)) return false;

                	return true;
            	}
		else if (Runtime.Trigger == TriggerType.Application)
            	{
                	// == Init ==
                	if (operation == "initialize")
                	{
                	    if (!Runtime.CheckWitness(Owner))
                	    {
                	        Runtime.Log("Owner signature verification failed!");
                	        return false;
                	    }
                	    if (args.Length != 3) return false;
                	    return Initialize((BigInteger)args[0], (BigInteger)args[1], (byte[])args[2]);
                	}

                	// == Getters ==
			// Going to have to modify to add our getters
                	if (operation == "getState") return GetState();
                	if (operation == "getMakerFee") return GetMakerFee(Empty);
                	if (operation == "getTakerFee") return GetTakerFee(Empty);
                	if (operation == "getExchangeRate") return GetExchangeRate((byte[])args[0]);
                	if (operation == "getOffers") return GetOffers((byte[])args[0], (byte[])args[1]);
                	if (operation == "getBalance") return GetBalance((byte[])args[0], (byte[])args[1]);

                	// == Execute ==
                	if (operation == "deposit")
                	{
                	    if (GetState() != Active) return false;
                	    if (args.Length != 3) return false;
                	    if (!VerifySentAmount((byte[])args[0], (byte[])args[1], (BigInteger)args[2])) return false;
                	    TransferAssetTo((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                	    return true;
                	}
                	if (operation == "makeOffer")
                	{
                	    if (GetState() != Active) return false;
                	    if (args.Length != 6) return false;
                	    var offer = NewOffer((byte[])args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3], (byte[])args[4], (byte[])args[2], (byte[])args[5]);
                	    return MakeOffer(offer);
                	}	
		}
	}
}
