# Rest Calls required

CreatePool:

	-args:
	
		MakerAddress #Neo address of current owner
		PoolID #Suffenctly random ID
		PoolCategory #TBD
		PoolAssetID #Neo assetID
		MaxPool #Max amount of Neo
		MinDeposit #Min Neo that can be deposited, default 1
		MaxDeposit #Max Neo that can be deposited, default 1
		CurrentSize #Current size of pool, default 1
		Amount #TBD
		StartTime #Time Pool starts accepting 
		EndTime #Time Pool ends, if not full already
		MakerCommand #Command that creator can send to have action taken
		Nonce 
		Epoch #Current Epoch
		
UpdatePool:

	-args:	
	
		MakerAddress #Neo address of current owner
		PoolID #ID of pool to be updated
		MakerCommand #Command that creator can send
		Nonce
		Epoch #Current Epoch

CompletePool:

	-args:
	
		MakerAddress #Neo address of current owner
		PoolID #ID of pool to be completed
		MakerCommand #Command that creator can send
		Nonce
		Epoch #Current Epoch

MakeContribution:

	-args:
	
		contribAddress #Neo address of current owner
		assetID #Neo assetID
		poolID #ID of pool for contribution
		amount #Deposit amount
		nonce 
		epoch #Current Epoch

CancelContribution:

	-args:
	
		contribAddress #Neo address of current owner
		assetID #NEO assetID
		contribID #ID of contribution
		poolID #ID of pool for contribution
		nonce
		epoch #Current Epoch
