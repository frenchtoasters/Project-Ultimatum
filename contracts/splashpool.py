"""
============================================

Deployment in neo-python:

import contract mct-dapp-splashpool.avm 0710 05 False False
wallet tkn_send MCT {from address} {dApp contract address} {minimum stake}

"""
from boa.interop.Neo.Runtime import GetTrigger, CheckWitness
from boa.interop.Neo.TriggerType import Application, Verification
from boa.interop.System.ExecutionEngine import GetExecutingScriptHash, GetCallingScriptHash, GetScriptContainer
from boa.interop.Neo.App import RegisterAppCall
from boa.interop.Neo.Transaction import TransactionInput, TransactionOutput, Transaction, ContractTransaction

OWNER = b'\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00\x00'

# mainnet
#MCT_SCRIPTHASH = b'?\xbc`|\x12\xc2\x87642$\xa4\xb4\xd8\xf5\x13\xa5\xc2|\xa8'
# privatenet
MCT_SCRIPTHASH = b'\x8dKL\x14V4\x17\xc6\x91\x91\xe0\x8b\xe0\xb8m\xdc\xb4\xbc\x86\xc1'

# mainnet
#MCTContract = RegisterAppCall('a87cc2a513f5d8b4a42432343687c2127c60bc3f', 'operation', 'args')
# privatenet
MCTContract = RegisterAppCall('c186bcb4dc6db8e08be09191c6173456144c4b8d', 'operation', 'args')

def Main(operation, args):
	trigger = GetTrigger()

	if trigger == Verification():
		'''
		Check if state of contract == {Active,Pending,Frozen}
		Validate inputs
		Check that valid self send
		Validate utxo has been reserved
		Validate withdraw destination
		Validate amount withdrawn
		'''
		if CheckWitness(OWNER):
			return True
		return False

	elif trigger == Application():

		if operation == 'initialize':
				if not CheckWitness(OWNER):
					print("initialize error")
					return False
				if len(args) != 3:
					print("To few args")
					return False
				return Initialize()

		if opertion == 'getState':
			return GetState()
		if operation == 'getBalance':
			return GetBalance(args[0],args[1])
		#if operation == 'getPool':
		#	return GetPool(args[0],args[1],args[2])
		if operation == 'deopsit':
			if GetState() != 'Active':
				return False
			if len(args) != 3:
				return False
			#if not VerifySentAmount(args[0],args[1],args[2]):
			#	return False
			if not TransferAssetTo(args[0],args[1],args[2]):
				return False
			return True
		if operation == 'makeContribution':
			if GetState() != 'Active':
				return False
			if len(args) != 6:
				return False
	
			contribution = NewContribution(operation,args)
			return MakeContribution(contribution)
		
		if operation == 'completePool':
			if GetState() != 'Active':
				return False
	
			if len(args) != 5:
				return False

			completePool = NewCompletePool(args[0],args[1],args[2],args[3])
			pool = GetPool(completePool['PoolID'],completePool['PoolHash'],completePool)
			return StorePool(completePool['PoolID'],completePool['PoolHash'],pool)

		if operation == 'withdraw':
			return ProcessWithdrawal()

		#Owner Operations
		if not CheckWitness(OWNER):
			return False

		if operation == 'freezePools':
			Put("state",'Inactive')
			return True

		if operation == 'unfreezePools':
			Put("state",'Active')
			return True
		
		if operation == 'addToWhitelist':
			if len(args) != 1:
				return False
			if Get('stateContractWhitelist') == 'Inactive':
				return False
			whitelistKey = args[0]+args[1]
			Put(whitelistKey,'1')
			return True
		'''
			return False
			if args[1] == 'single':
				return RemoveSingleWhitelist(args)
			if args[1] == 'all':
				Put('stateContractWhitelist','Inactive')
			return True
		'''
		if operation == 'ownerWithdraw':
			if not CheckWitness(OWNER):
				print('Only the contract owner can withdraw MCT from the contract')
				return False

			if len(args) != 1:
				print('withdraw amount not specified')
				return False
        
			t_amount = args[0]
			myhash = GetExecutingScriptHash()

			return MCTContract('transfer', [myhash, OWNER, t_amount])

		# end of normal invocations, reject any non-MCT invocations

		caller = GetCallingScriptHash()
	
		if caller != MCT_SCRIPTHASH:
			print('token type not accepted by this contract')
			return False

		if operation == 'onTokenTransfer':
			print('onTokenTransfer() called')
			return handle_token_received(caller, args)
	return False
def Initialize():
	Put('state','Active')
	return True

def GetState():
	return Get('state')

def GetBalance(originator, assetId):
	balanceKey = originator + assetId
	return Get(balanceKey)

def GetPool(poolID, poolHash, pool):
	poolID = args[0]
	poolHash = args[1]
	pool = args[2]
	if len(pool) == 0:
		return NewPool()
	
	print('Deserializing offer')
	#Figure out what deserializing will be needed
	return pool
'''
def VerifySentAmount(originator, assetId, amount):
	if len(assetId) == 32:
		#Verify that the offer really has the indicated assets available
		mycontainer = GetScriptContainer()
		#Get outputs of tx
		outputs = 
		#sentAmount = 0
		#for i in outputs:
		#	if i.assetId == assetId and i.ScriptHash == mycontainer['ExecutingScriptHash']:
		#		sentAmount += i.Value
	
	#if sentAmount != amount:
	#	return False

	alreadyVerified = Get(mycontainer['hash']+assetId)
	if alreadyVerified:
		return False
	
	Put(mycontainer['hash']+assetId, '1')

	return True
'''
def TransferAssetTo(originator, assetID, amount):
	if amount < 1:
		print('Amount ot transfer is less than 1')
		return False

	key = originator+assetID
	currentBalance = Get(key)
	Put(key,currentBalance + amount)
	return True

def NewContribution(operation, args):
	#Create New contribution
	#return object Contribution

	return True

def Hash(contribution):
	#Will need to return hash256 of poolid, amount as byte array, nonce
	return True

def MakeContribution(contribution):
	if not CheckWitness(contribution['owner']):
		return False
	
	poolID = contribution['poolID']
	contributionHash = Hash(contribution)
	pool = GetPool(poolID)

	if Get(poolID+contributionHash) != None:
		return False
	
	if not contribution['amount'] > 0:
		return False

	if not contribution['amount'] > pool['MinDeposit']:
		return False
	
	if contribution['amount'] + pool['CurrentSize'] > pool['MaxSize']:
		 return False

	if not ReduceBalance(contribution['makerAddress'],poolID,contributionHash,contribution['amount']):
		return False

	#Add the contribution to storage
	StorePool(poolID,contributionHash,pool)

	#Notify Clients
	Created(contribution['makerAddress'],contributionHash,contribution['contributionAssetID'],contribution['amount'])
	return True

def NewCompletePool(operatorAddress, poolID, poolHash, amountToFill):
	#Update Pool such that status is now Complete
	pool = GetPool(poolID,poolHash)
	
	if not CheckWitness(operatorAddress):
		return False

	if pool['MakerAddress'] == None:
		#Notify failed
		#Failed(operatorAddress, poolHash)
		return True
	
	if operatorAddress != pool['MakerAddress']:
		return False
	
	#Move Asset
	TransferAssetTo(operatorAddress,pool['PoolAssetID'],amountToFill)
	
	#Notify Clients
	#Transferred(operatorAddress,pool['PoolAssetID'],amountToFill)

	#Store Pool
	StorePool(poolID,poolHash,pool)

	return True

def StorePool(poolID, poolHash, pool):
	if not Put(poolID+poolHash, pool):
		return False

def ProcessWithdrawal():
	mycontainer = GetScriptContainer()
	myhash = GetExecutingScriptHash()
	withdrawlStage = WithdrawStage(mycontainer)
	if withdrawlStage == None:
		return False

	withdrawingAddr = GetWithdrawAddress(mycontainer, withdrawlStage)
	assetID = GetWithdrawlAsset(mycontainer)
	if len(assetID) == 20:
		isWithdrawingNEP5 = True
	else:
		isWithdrawingNEP5 = False
	
	#inputs = mycontainer.GetInputs()
	#outputs = mycontainer.GetOutputs()
	
	if withdrawlStage == 'Mark':
		amount = GetBalance(withdrawingAddr, assetID)
		#Here you can add withdraw fees and things like that
		MarkWithdrawal(withdrawingAddr, assetID, amount)

	byteArray = ['0']
	if isWithdrawingNEP5:
		Put(myhash+byteArray,withdrawingAddr)
	else:
		value = 0
		for output in outputs:
			value += outputs[output]['Value']
	
	Withdrawing(withdrawingAddr, assetID, amount)
	return True

def WhitelistKey(assetID):
	if len(assetID) != 1:
		return False
	whitelistID="contractWhitelist"+assetID
	return whitelistID

def RemoveSingleWhitelist(assetID):
	#This is a function to remove a single assetID that was passed from whitelist
	#Need to figure out later
	return True

def ReduceBalance(makerAddress, poolID, contirbutionHash, amount):
	if amount < 1:
		print("Amount to reduce is less than 1")
		return False
	
	key = makerAddress + assetID
	currentBalance = Get(key)
	newBalance = currentBalance - amount

	if newBalance < 0:
		print("Not Enough")
		return False
	
	if newBalance > 0:
		Put(key,newBalance)
	else:
		Delete(key)

	return True

def Created(makerAddress, contributionHash, contributionAssetID, amount):
	#Used to notify clients of event
	#Will need futher research to complete in python
	return True

def NewPool(): #returns namedtuple, json, something refrencable like pool['makerAddress']
	#This will need to create a new array that can be passed 
	#Will require further research
	return True

def WithdrawStage(mycontainer):
	#will need to verify that the container object is correct object being sent
	#Really just needs to be the tx
	#txAttributes = mycontainer.GetAttributes()
	#for attr in txAttributes:
	#	if attr.Usage == '0xa1':
	#		return attr.Data
	return None

def GetWithdrawAddress(mycontainer, withdrawalStage):
	usage = withdrawalStage
	extraWitness = '0x20'
	withdrawalAddress = '0xa4'
	#txAttributes = mycontainer.GetAttributes()
	#for attr in txAttributes:
	#	if attr.Usage in NEP5AssetID:
	#		return attr.Data
	return None

def GetWithdrawlAsset(mycontainer):
	#txAttributes = mycontainer.GetAttributes()
	#for attr in txAttributes:
	#	if attr.Usage in NEP5AssetID:
	#		return attr.Data
	return None

def MarkWithdrawal(withdrawingAddr, assetID, amount):
	print("Checking last mark")
	#if not VerifyWithdrawal(withdrawingAddr, assetID):
	#	return False
	
	print("Marking withdrawal")
	if not ReduceBalance(withdrawingAddr, assetID, amount):
		return False
	
	key = withdrawingAddr+assetID
	Put(key,amount)

	return True

def Withdrawing(withdrawingAddr, assetID, amount):
	#This is another of the notify type actions
	#The ones that have the displaynames
	return True

def handle_token_received(chash, args):

    if len(args) < 3:
        print('arg length incorrect')
        return False

    t_from = args[0]
    t_to = args[1]
    t_amount = args[2]

    if len(args) == 4:
        extra_arg = args[3]  # extra argument passed by transfer()

    if len(t_from) != 20:
        return False

    if len(t_to) != 20:
        return False

    myhash = GetExecutingScriptHash()

    if t_to != myhash:
        return False

    if t_from == OWNER:
        # topping up contract token balance, just return True to allow
        return True

    if extra_arg == 'reject-me':
        print('rejecting transfer') 
        Delete(t_from)
        return False
    else:
        print('received MCT tokens!')
        totalsent = Get(t_from)
        totalsent = totalsent + t_amount
        if Put(t_from, totalsent):
            return True
        print('staked storage call failed')
        return False


# Staked storage appcalls

def Get(key):
    return MCTContract('Get', [key])

def Delete(key):
    return MCTContract('Delete', [key]) 

def Put(key, value):
    return MCTContract('Put', [key, value])

