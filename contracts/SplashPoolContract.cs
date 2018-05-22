using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace splashpool
{
    public class SplashPool : SmartContract
    {
        //public delegate object NEP5Contract(string method, object[] args);

        // Events
        [DisplayName("created")]
        public static event Action<byte[], byte[], byte[], BigInteger, byte[], BigInteger> Created; // (address, offerHash, offerAssetID, offerAmount, wantAssetID, wantAmount)

        [DisplayName("filled")]
        public static event Action<byte[], byte[], BigInteger, byte[], BigInteger, byte[], BigInteger> Filled; // (address, offerHash, fillAmount, offerAssetID, offerAmount, wantAssetID, wantAmount)

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

        // Broker Settings & Hardcaps
        private static readonly byte[] Owner = "Ae6LkR5TLXVVAE5WSRqAEDEYBx6ChBE6Am".ToScriptHash();
        private static readonly byte[] NativeToken = "AbwJtGDCcwoH2HhDmDq12ZcqFmUpCU3XMp".ToScriptHash();
        private const ulong feeFactor = 1000000; // 1 => 0.0001%
        private const int maxFee = 5000; // 5000/1000000 = 0.5%
        private const int bucketDuration = 82800; // 82800secs = 23hrs
        private const int nativeTokenDiscount = 2; // 1/2 => 50%

        // Contract States
        private static readonly byte[] Pending = { };         // only can initialize
        private static readonly byte[] Active = { 0x01 };     // all operations active
        private static readonly byte[] Inactive = { 0x02 };   // trading halted - only can do cancel, withdrawl & owner actions

        // Asset Categories
        private static readonly byte[] SystemAsset = { 0x99 };
        private static readonly byte[] NEP5 = { 0x98 };

        // Withdrawal Flags
        private static readonly byte[] Mark = { 0x50 };
        private static readonly byte[] Withdraw = { 0x51 };
        private static readonly byte[] OpCode_TailCall = { 0x69 };
        private static readonly byte Type_InvocationTransaction = 0xd1;
        private static readonly byte TAUsage_WithdrawalStage = 0xa1;
        private static readonly byte TAUsage_NEP5AssetID = 0xa2;
        private static readonly byte TAUsage_SystemAssetID = 0xa3;
        private static readonly byte TAUsage_WithdrawalAddress = 0xa4;
        private static readonly byte TAUsage_AdditionalWitness = 0x20; // additional verification script which can be used to ensure any withdrawal txns are intended by the owner

        // Byte Constants
        private static readonly byte[] Empty = { };
        private static readonly byte[] Zeroes = { 0, 0, 0, 0, 0, 0, 0, 0 }; // for fixed8 (8 bytes)
        private static readonly byte[] Null = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // for fixed width list ptr (32bytes)        
        private static readonly byte[] NeoAssetID = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private static readonly byte[] GasAssetID = { 231, 45, 40, 105, 121, 238, 108, 177, 183, 230, 93, 253, 223, 178, 227, 132, 16, 11, 141, 20, 142, 119, 88, 222, 66, 228, 22, 139, 113, 121, 44, 96 };
        private static readonly byte[] WithdrawArgs = { 0x00, 0xc1, 0x08, 0x77, 0x69, 0x74, 0x68, 0x64, 0x72, 0x61, 0x77 }; // PUSH0, PACK, PUSHBYTES8, "withdraw" as bytes

        private struct Pool
        {
            public byte[] MakerAddress;
            public byte[] PoolID;
            public byte[] PoolCategory;
	    public byte[] PoolAssetID;
            public BigInteger MaxPool;
            public BigInteger MinDeposit;
	    public BigInteger CurrentSize;
	    public BigInteger Amount;
            public byte[] StartTime;
	    public byte[] EndTime;
	    public byte[] MakerCommand;
            public byte[] Nonce;
	    public byte[] CurrentEpoch;
        }

	/*
	 * Dont know if i need this or not yet
        private struct Volume
        {
            public BigInteger Native;
            public BigInteger Foreign;
        }
	*/
        private static Pool NewContribution(
            byte[] makerAddress,
	    byte[] assetID,
            byte[] poolID, 
	    byte[] amount,
            byte[] nonce,
	    byte[] epoch
        )
        {
		//Not currently needed only doing NEO
	/*
            var offerAssetCategory = NEP5; //Byte code of Assest Type
            var wantAssetCategory = NEP5; //Byte code of Asset Type
            if (offerAssetID.Length == 32) offerAssetCategory = SystemAsset;
            if (wantAssetID.Length == 32) wantAssetCategory = SystemAsset;
	*/

            /* Here we need to verify:
	     * if PoolID[StartTime] == epoch
	     * if PoolID[MaxPool] <= Pool[CurrentSize]
	     * Return
	     */ 
            return new Pool
            {
                MakerAddress = makerAddress.Take(20),
                PoolID = poolID,
                PoolCategory = PoolCategory,
		PoolAssetID = assetID,
                Amount = amount.AsBigInteger(),
                Nonce = nonce,
		CurrentEpoch = epoch,
            };
        }

        /// <summary>
        ///   This is the SplashPool smart contract entrypoint.
        /// 
        ///   Parameter List: 0710
        ///   Return List: 05
        /// </summary>
        /// <param name="operation">
        ///   The method to be invoked.
        /// </param>
        /// <param name="args">
        ///   Input parameters for the delegated method.
        /// </param>
        public static object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (GetState() == Pending) return false;

                var currentTxn = (Transaction)ExecutionEngine.ScriptContainer;
		//Might need to look back at these withdraw values
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
		    // Need to update VerifyWithdrawal
		    // Inputs:
		    // PoolID
		    // OperatorMsg
		    // OperatorKey
                    if (!VerifyWithdrawal(withdrawingAddr, assetID)) return false;

                    // Check that inputs are not already reserved
		    // Dont think need to change this at all
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
                if (operation == "getState") return GetState();
                if (operation == "getMakerFee") return GetMakerFee(Empty);
                if (operation == "getTakerFee") return GetTakerFee(Empty);
                if (operation == "getExchangeRate") return GetExchangeRate((byte[])args[0]);
                if (operation == "getOffers") return GetOffers((byte[])args[0], (byte[])args[1]);
                if (operation == "getBalance") return GetBalance((byte[])args[0], (byte[])args[1]);

                // == Execute ==
                if (operation == "deposit")
			//Standard Deposit into the contract by participant
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 3) return false;
                    if (!VerifySentAmount((byte[])args[0], (byte[])args[1], (BigInteger)args[2])) return false;
                    TransferAssetTo((byte[])args[0], (byte[])args[1], (BigInteger)args[2]);
                    return true;
                }
                if (operation == "makeContribution")
			//Where the user deposits their coins for contribution
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 6) return false;
                    var contribution = NewContribution((byte[])args[0], (byte[])args[1], (byte[])args[2], (byte[])args[3], (byte[])args[4], (byte[])args[5]);
                    return MakeContribution(contribution);
                }
                if (operation == "completePool")
                {
                    if (GetState() != Active) return false;
                    if (args.Length != 5) return false;
                    return CompletePool((byte[])args[0], (byte[])args[1], (byte[])args[2], (BigInteger)args[3], (bool)args[4]);
                }
                if (operation == "cancelContribution")
                {
                    if (GetState() == Pending) return false;
                    if (args.Length != 2) return false;
                    return CancelContribution((byte[])args[0], (byte[])args[1]);
                }
		if (operation == "updatePool")
		{
		    //Verify operator_key && operator_msg -> decryptBy(prev_operator_msg) 
		}
		if (operation == "createPool")
		{
		    //Where all the storage puts happen and the 
		}
                if (operation == "withdraw")
                {
                    return ProcessWithdrawal();
                }

                // == Owner ==
                if (!Runtime.CheckWitness(Owner))
                {
                    Runtime.Log("Owner signature verification failed");
                    return false;
                }
                if (operation == "freezePools")
                {
                    Storage.Put(Context(), "state", Inactive);
                    return true;
                }
                if (operation == "unfreezePools")
                {
                    Storage.Put(Context(), "state", Active);
                    return true;
                }
                if (operation == "setMakerFee")
                {
                    if (args.Length != 2) return false;
                    return SetMakerFee((BigInteger)args[0], (byte[])args[1]);
                }
                if (operation == "setTakerFee")
                {
                    if (args.Length != 2) return false;
                    return SetTakerFee((BigInteger)args[0], (byte[])args[1]);
                }
                if (operation == "setFeeAddress")
                {
                    if (args.Length != 1) return false;
                    return SetFeeAddress((byte[])args[0]);
                }
                if (operation == "addToWhitelist")
                {
                    if (args.Length != 1) return false;
                    if (Storage.Get(Context(), "stateContractWhitelist") == Inactive) return false;
                    Storage.Put(Context(), WhitelistKey((byte[])args[0]), "1");
                }
                if (operation == "destroyWhitelist")
                {
                    Storage.Put(Context(), "stateContractWhitelist", Inactive);
                }
		/*
		 *
		 * Probably need more functions here but ok for now
		 *
		 */
            }

            return true;
        }

        private static bool Initialize(BigInteger takerFee, BigInteger makerFee, byte[] feeAddress)
        {
            if (GetState() != Pending) return false;
            if (!SetMakerFee(makerFee, Empty)) return false;
            if (!SetTakerFee(takerFee, Empty)) return false;
            if (!SetFeeAddress(feeAddress)) return false;

            Storage.Put(Context(), "state", Active);

            Runtime.Log("Contract initialized");
            return true;
        }

        private static byte[] GetState()
        {
            return Storage.Get(Context(), "state");
        }

        private static BigInteger GetMakerFee(byte[] assetID)
        {
            var fee = Storage.Get(Context(), "makerFee".AsByteArray().Concat(assetID));
            if (fee.Length != 0 || assetID.Length == 0) return fee.AsBigInteger();

            return Storage.Get(Context(), "makerFee").AsBigInteger();
        }

        private static BigInteger GetTakerFee(byte[] assetID)
        {
            var fee = Storage.Get(Context(), "takerFee".AsByteArray().Concat(assetID));
            if (fee.Length != 0 || assetID.Length == 0) return fee.AsBigInteger();

            return Storage.Get(Context(), "takerFee").AsBigInteger();
        }

        private static BigInteger GetBalance(byte[] originator, byte[] assetID)
        {
            return Storage.Get(Context(), BalanceKey(originator, assetID)).AsBigInteger();
        }

        private static BigInteger GetWithdrawAmount(byte[] originator, byte[] assetID)
        {
            return Storage.Get(Context(), WithdrawKey(originator, assetID)).AsBigInteger();
        }

        private static Volume GetExchangeRate(byte[] assetID) // against native token
        {
            var bucketNumber = CurrentBucket();
            return GetVolume(bucketNumber, assetID);
        }

        private static Pool[] GetContributions(byte[] poolId, byte[] offset) // offerAssetID.Concat(wantAssetID)
        {
            var result = new Pool[50];

            var it = Storage.Get(Context(), poolId);

            while (it.Next())
            {
                if (it.Value == offset) break;
            }

            var i = 0;
            while (it.Next() && i < 50)
            {
                var value = it.Value;
                var bytes = value.Deserialize();
                var pool = (Pool)bytes;
                result[i] = pool;
                i++;
            }

            return result;
        }

        private static bool MakeContribution(Pool pool)
        {
		/*
		 *
		 * This function is where we put the Contribution that the user has made into storage
		 *
		 */
            // Check that transaction is signed by the maker
            if (!Runtime.CheckWitness(pool.MakerAddress)) return false;

            // Check that nonce is not repeated
	    // This section is just checking to see if duplicate contribution is going to be made
	    // Will need to update slightly for TradingPair to get poolID
            var poolId = Storage.Get(Context(), pool.PoolId);
            var poolHash = Hash(pool);
            if (Storage.Get(Context(), poolId.Concat(poolHash)) != Empty) return false;

            // Check that the amounts > 0
            if (!(pool.Amount > 0)) return false;

	    //Check that Amount !> Min
	    if (!(pool.Amount > Storage.Get(Context(), poolId.MinDeposit))) return false;

	    //Check that Amount !> Max
	    maxPool = Storage.Get(Context(), poolId.MaxPool);
            currentPool = Storage.Get(Context(), poolId.CurrentSize);
	    if (!((pool.Amount + currentPool)) > maxPool) return false;

            // Check the trade is across different assets
	    // Dont think this is needed
            //if (offer.OfferAssetID == offer.WantAssetID) return false;

            // Check that asset IDs are valid
	    // Dont think this is needed
            //if ((offer.OfferAssetID.Length != 20 && offer.OfferAssetID.Length != 32) ||
              //  (offer.WantAssetID.Length != 20 && offer.WantAssetID.Length != 32)) return false;

            // Reduce available balance for the offered asset and amount
            if (!ReduceBalance(pool.MakerAddress, pool.PoolAssetID, pool.Amount)) return false;

            // Add the contribution to storage
            StoreOffer(poolId, poolHash, pool);

            // Notify clients
            Created(pool.MakerAddress, poolHash, pool.PoolAssetID, pool.Amount);
            return true;
        }

        private static bool CompletePool(byte[] operatorAddress, byte[] poolID, byte[] poolHash, BigInteger amountToFill, bool useNativeTokens)
        {
		/*
		 *
		 * This function is where we verify that the Pool has closed and send the amount to the PoolOwner
		 * Inputs:
		 * Operator_Key
		 * Operator_Msg
		 * PoolID
		 * poolHash //this is where the MakerCommand value will come from 
		 *
		 */
            // Check that transaction is signed by the operator
            if (!Runtime.CheckWitness(operatorAddress)) return false;

            // Check that the pool still exists
            Pool pool = GetContributions(poolID, offerHash);
            if (pool.MakerAddress == Empty)
            {
                // Notify clients of failure
                Failed(operatorAddress, poolHash);
                return true;
            }

	    // This is where we need to change verification
	    // Make sure the operatorAddress is same as MakerAddress
            if (operatorAddress != pool.MakerAddress) return false;

	    /*
	     * Here is where we figure out Fee stuff, im not really a fan of 
	     * Fee's if we can get awawy with it. 
	     *
	     *
	    

            // Calculate offered amount and fees
            byte[] feeAddress = Storage.Get(Context(), "feeAddress");
            BigInteger makerFeeRate = GetMakerFee(offer.WantAssetID);
            BigInteger takerFeeRate = GetTakerFee(offer.OfferAssetID);
            BigInteger makerFee = (amountToFill * makerFeeRate) / feeFactor;
            BigInteger takerFee = (amountToTake * takerFeeRate) / feeFactor;
            BigInteger nativeFee = 0;

            // Calculate Fees If Any
            if (offer.OfferAssetID == NativeToken) {
                nativeFee = takerFee / nativeTokenDiscount;
            }
	    */
	    
	    /*
	     *
	     * Need to add verification of currentEpoch and EndTime?
	     * Need to add verification of MakerCommand
	     *
	     */


            // Move asset to the operator balance and notify clients
            //var takerAmount = amountToTake - (nativeFee > 0 ? 0 : takerFee);
	    TransferAssetTo(operatorAddress, pool.PoolAssetID, amountToFill);
	    Transferred(operatorAddress, pool.PoolAssetID, amountToFill);

            // Move asset back to the user when deposited and notify clients
            // 
	    // This is going to need some work for later, will require going through
	    // all the deposits and gathering amounts made. 
	    //
	    // THIS REQUIRES NEW VAR IN Pool STRUCT!!!!!!!!!!!!!!!!!!!!!!!!!
	    //
            //TransferAssetTo(offer.MakerAddress, offer.WantAssetID, makerAmount);
            //Transferred(offer.MakerAddress, offer.WantAssetID, makerAmount);

            // Move fees
            //if (makerFee > 0) TransferAssetTo(feeAddress, offer.WantAssetID, makerFee);
            //if (nativeFee == 0) TransferAssetTo(feeAddress, offer.OfferAssetID, takerFee);

            // Update native token exchange rate
	    /*
            if (offer.OfferAssetID == NativeToken)
            {
                AddVolume(offer.WantAssetID, amountToFill, amountToTake);
            }
            if (offer.WantAssetID == NativeToken)
            {
                AddVolume(offer.OfferAssetID, amountToTake, amountToFill);
            }
	    */

            // Update pool status
            //pool.PoolCategory = NOT SURE WHAT TO PUT HERE;

            // Store updated offer
            StorePool(operatorAddress, poolHash, pool);

            // Notify clients
            Filled(operatorAddress, poolHash, amountToFill, pool.PoolAssetID, pool.Amount);
            return true;
        }

        private static bool CancelOffer(byte[] poolID, byte[] poolHash)
        {
            // Check that the offer exists
            Pool pool = GetContributions(poolID, poolHash);
            if (pool.MakerAddress == Empty) return false;

            // Check that transaction is signed by the canceller
            if (!Runtime.CheckWitness(pool.MakerAddress)) return false;

            // Return Funds to contributors
	    //
	    // This is going to need some work.
	    // Need to figure out how to store their recipts first.
	    //
            // TransferAssetTo(pool.MakerAddress, pool.PoolAssetID, pool.AvailableAmount);

            // Remove offer
            RemovePool(poolID, poolHash);

            // Notify runtime
            Cancelled(pool.MakerAddress, poolHash);
            return true;
        }
        
	/*	
        private static bool SetMakerFee(BigInteger fee, byte[] assetID)
        {
            if (fee > maxFee) return false;
            if (fee < 0) return false;

            Storage.Put(Context(), "makerFee".AsByteArray().Concat(assetID), fee);

            return true;
        }

        private static bool SetTakerFee(BigInteger fee, byte[] assetID)
        {
            if (fee > maxFee) return false;
            if (fee < 0) return false;

            Storage.Put(Context(), "takerFee".AsByteArray().Concat(assetID), fee);

            return true;
        }

        private static bool SetFeeAddress(byte[] feeAddress)
        {
            if (feeAddress.Length != 20) return false;
            Storage.Put(Context(), "feeAddress", feeAddress);

            return true;
        }
	*/
        private static object ProcessWithdrawal()
        {
            var currentTxn = (Transaction)ExecutionEngine.ScriptContainer;
            var withdrawalStage = WithdrawalStage(currentTxn);
            if (withdrawalStage == Empty) return false;

            var withdrawingAddr = GetWithdrawalAddress(currentTxn, withdrawalStage);
            var assetID = GetWithdrawalAsset(currentTxn);
            var isWithdrawingNEP5 = assetID.Length == 20;
            var inputs = currentTxn.GetInputs();
            var outputs = currentTxn.GetOutputs();

            if (withdrawalStage == Mark)
            {
                var amount = GetBalance(withdrawingAddr, assetID);
                if (assetID == NeoAssetID)
                {
                    // neo must be rounded down
                    const ulong neoAssetFactor = 100000000;
                    amount = amount / neoAssetFactor * neoAssetFactor; 
                }

                MarkWithdrawal(withdrawingAddr, assetID, amount);

                if (isWithdrawingNEP5)
                {
                    Storage.Put(Context(), currentTxn.Hash.Concat(IndexAsByteArray(0)), withdrawingAddr);
                }
                else
                {
                    ulong sum = 0;
                    for (ushort index = 0; index < outputs.Length; index++)
                    {
                        sum += (ulong)outputs[index].Value;
                        if (sum <= amount)
                        {
                            Storage.Put(Context(), currentTxn.Hash.Concat(IndexAsByteArray(index)), withdrawingAddr);
                        }
                    }
                }

                Withdrawing(withdrawingAddr, assetID, amount);
                return true;
            }
            else if (withdrawalStage == Withdraw)
            {
                foreach (var i in inputs)
                {
                    Storage.Delete(Context(), i.PrevHash.Concat(IndexAsByteArray(i.PrevIndex)));
                }

                var amount = GetWithdrawAmount(withdrawingAddr, assetID);
                if (isWithdrawingNEP5 && !WithdrawNEP5(withdrawingAddr, assetID, amount)) return false;

                Storage.Delete(Context(), WithdrawKey(withdrawingAddr, assetID));
                Withdrawn(withdrawingAddr, assetID, amount);
                return true;
            }

            return false;
        }

        private static bool VerifyWithdrawal(byte[] holderAddress, byte[] assetID)
        {
            var balance = GetBalance(holderAddress, assetID);
            if (balance <= 0) return false;

            var withdrawingAmount = GetWithdrawAmount(holderAddress, assetID);
            if (withdrawingAmount > 0) return false;

            return true;
        }

        private static bool VerifySentAmount(byte[] originator, byte[] assetID, BigInteger amount)
        {
            // Verify that the offer really has the indicated assets available
            if (assetID.Length == 32)
            {
                // Check the current transaction for the system assets
                var currentTxn = (Transaction)ExecutionEngine.ScriptContainer;
                var outputs = currentTxn.GetOutputs();
                ulong sentAmount = 0;
                foreach (var o in outputs)
                {
                    if (o.AssetId == assetID && o.ScriptHash == ExecutionEngine.ExecutingScriptHash)
                    {
                        sentAmount += (ulong)o.Value;
                    }
                }

                // Check that the sent amount is correct
                if (sentAmount != amount)
                {
                    return false;
                }

                // Check that there is no double deposit
                var alreadyVerified = Storage.Get(Context(), currentTxn.Hash.Concat(assetID)).Length > 0;
                if (alreadyVerified) return false;

                // Update the consumed amount for this txn
                Storage.Put(Context(), currentTxn.Hash.Concat(assetID), 1);

                // TODO: how to cleanup?
                return true;
            }
            else if (assetID.Length == 20)
            {
                // Just transfer immediately or fail as this is the last step in verification
                if (!VerifyContract(assetID)) return false;
                var args = new object[] { originator, ExecutionEngine.ExecutingScriptHash, amount };
                var Contract = (NEP5Contract)assetID.ToDelegate();
                var transferSuccessful = (bool)Contract("transfer", args);
                return transferSuccessful;
            }

            // Unknown asset category
            return false;
        }

        private static bool VerifyContract(byte[] assetID)
        {
            if (Storage.Get(Context(), "stateContractWhitelist") == Inactive) return true;
            return Storage.Get(Context(), WhitelistKey(assetID)).Length > 0;
        }

        private static Pool GetPool(byte[] poolID, byte[] hash)
        {
            byte[] poolData = Storage.Get(Context(), poolID.Concat(hash));
            if (poolData.Length == 0) return new Pool();

            Runtime.Log("Deserializing offer");
            return (Pool)poolData.Deserialize();
        }

        private static void StorePool(byte[] poolID, byte[] poolHash, Pool pool)
        {
            // Remove pool if completely filled
            if (pool.CurrentSize == pool.MaxPool)
            {
                RemovePool(poolID, poolHash);
            }
            // Store offer otherwise
            else
            {
                // Serialize offer
                Runtime.Log("Serializing offer");
                var poolData = pool.Serialize();
                Storage.Put(Context(), poolID.Concat(poolHash), poolData);
            }
        }

        private static void RemoveOffer(byte[] poolID, byte[] poolHash)
        {
            // Delete offer data
            Storage.Delete(Context(), poolID.Concat(poolHash));
        }

        private static void TransferAssetTo(byte[] originator, byte[] assetID, BigInteger amount)
        {
            if (amount < 1)
            {
                Runtime.Log("Amount to transfer is less than 1!");
                return;
            }

            byte[] key = BalanceKey(originator, assetID);
            BigInteger currentBalance = Storage.Get(Context(), key).AsBigInteger();
            Storage.Put(Context(), key, currentBalance + amount);
        }

        private static bool ReduceBalance(byte[] address, byte[] assetID, BigInteger amount)
        {
            if (amount < 1)
            {
                Runtime.Log("Amount to reduce is less than 1!");
                return false;
            }

            var key = BalanceKey(address, assetID);
            var currentBalance = Storage.Get(Context(), key).AsBigInteger();
            var newBalance = currentBalance - amount;

            if (newBalance < 0)
            {
                Runtime.Log("Not enough balance!");
                return false;
            }

            if (newBalance > 0) Storage.Put(Context(), key, newBalance);
            else Storage.Delete(Context(), key);

            return true;
        }

        private static bool MarkWithdrawal(byte[] address, byte[] assetID, BigInteger amount)
        {
            Runtime.Log("Checking Last Mark..");
            if (!VerifyWithdrawal(address, assetID)) return false;

            Runtime.Log("Marking Withdrawal..");  
            if (!ReduceBalance(address, assetID, amount)) return false;
            Storage.Put(Context(), WithdrawKey(address, assetID), amount);

            return true;
        }

        private static bool WithdrawNEP5(byte[] address, byte[] assetID, BigInteger amount)
        {
            // Transfer token
            if (!VerifyContract(assetID)) return false;
            var args = new object[] { ExecutionEngine.ExecutingScriptHash, address, amount };
            var contract = (NEP5Contract)assetID.ToDelegate();
            bool transferSuccessful = (bool)contract("transfer", args);
            if (!transferSuccessful)
            {
                Runtime.Log("Failed to transfer NEP-5 tokens!");
                return false;
            }

            return true;
        }

        private static byte[] GetWithdrawalAddress(Transaction transaction, byte[] withdrawalStage)
        {
            var usage = withdrawalStage == Mark ? TAUsage_AdditionalWitness : TAUsage_WithdrawalAddress;
            var txnAttributes = transaction.GetAttributes();
            foreach (var attr in txnAttributes)
            {
                if (attr.Usage == usage) return attr.Data.Take(20);
            }
            return Empty;
        }

        private static byte[] GetWithdrawalAsset(Transaction transaction)
        {
            var txnAttributes = transaction.GetAttributes();
            foreach (var attr in txnAttributes)
            {
                if (attr.Usage == TAUsage_NEP5AssetID) return attr.Data.Take(20);
                if (attr.Usage == TAUsage_SystemAssetID) return attr.Data;
            }
            return Empty;
        }

        private static byte[] WithdrawalStage(Transaction transaction)
        {
            var txnAttributes = transaction.GetAttributes();
            foreach (var attr in txnAttributes)
            {
                if (attr.Usage == TAUsage_WithdrawalStage) return attr.Data.Take(1);
            }
            return Empty;
        }

        private static BigInteger AmountToOffer(Offer o, BigInteger amount)
        {
            return (o.OfferAmount * amount) / o.WantAmount;
        }

        private static byte[] Hash(Offer o)
        {
            var bytes = o.MakerAddress
                .Concat(TradingPair(o))
                .Concat(o.OfferAmount.AsByteArray())
                .Concat(o.WantAmount.AsByteArray())
                .Concat(o.Nonce);

            return Hash256(bytes);
        }
	
	/*
        // Add volume to the current reference assetID e.g. NEO/SWH: Add nativeAmount to SWH volume and foreignAmount to NEO volume
        private static bool AddVolume(byte[] assetID, BigInteger nativeAmount, BigInteger foreignAmount) 
        {
            // Retrieve all volumes from current 24 hr bucket
            var bucketNumber = CurrentBucket();
            var volumeKey = VolumeKey(bucketNumber, assetID);
            byte[] volumeData = Storage.Get(Context(), volumeKey);

            Volume volume;

            // Either create a new record or add to existing volume
            if (volumeData.Length == 0)
            {
                volume = new Volume
                {
                    Native = nativeAmount,
                    Foreign = foreignAmount
                };
            }
            else
            {
                volume = (Volume)volumeData.Deserialize();
                volume.Native = volume.Native + nativeAmount;
                volume.Foreign = volume.Foreign + foreignAmount;
            }

            // Save to blockchain
            Storage.Put(Context(), volumeKey, volume.Serialize());
            Runtime.Log("Done serializing and storing");

            return true;
        }

        // Retrieves the native and foreign volume of a reference assetID in the current 24 hr bucket
        private static Volume GetVolume(BigInteger bucketNumber, byte[] assetID)
        {
            byte[] volumeData = Storage.Get(Context(), VolumeKey(bucketNumber, assetID));
            if (volumeData.Length == 0)
            {
                return new Volume();
            }
            else {
                return (Volume)volumeData.Deserialize();
            }
        }
	*/
        // Helpers
        private static StorageContext Context() => Storage.CurrentContext;
        private static BigInteger CurrentBucket() => Runtime.Time / bucketDuration;
        private static byte[] IndexAsByteArray(ushort index) => index > 0 ? ((BigInteger)index).AsByteArray() : Empty;
        private static byte[] TradingPair(Offer o) => o.OfferAssetID.Concat(o.WantAssetID);

        // Keys
        private static byte[] BalanceKey(byte[] originator, byte[] assetID) => originator.Concat(assetID);
        private static byte[] WithdrawKey(byte[] originator, byte[] assetID) => originator.Concat(assetID).Concat(Withdraw);
        private static byte[] WhitelistKey(byte[] assetID) => "contractWhitelist".AsByteArray().Concat(assetID);
        private static byte[] VolumeKey(BigInteger bucketNumber, byte[] assetID) => "tradeVolume".AsByteArray().Concat(bucketNumber.AsByteArray()).Concat(assetID);
    }
}
