 ###### Yanked code for workflow
 
 ```
    Create_Update_Pool
    Assumption: operator_msg will be like the password to the pool, it basically needs to be a signed message proving that the owner is who they say. Therefore they would need to sign the message with a private key that is theirs and theirs only. 
        Args:
            - operator_key == operator public key
            - operator_msg == signed message by owner
            - size
            - min
            - max
            - current
        Return:
            pool_id = init_random()
            find_pool(pool_id) ? False: current = 0
            - True ? operator_key 
            - False ? !operatror_key

```
     
    elif operation == 'startPool':
        if len(args) == 4:
            return start_pool(ctx, args[0], args[1], args[2], args[3])
    
    '''
    Deposit_Pool
        Args:
            - pool_id
            - size
        Return:
            max = Find_Pool(pool_id)
            - True ? size <= max
            - False ? size > max
     '''
    elif operation == 'depositPool':
        if len(args) == 2:
            return deposit_pool(ctx, args[0], args[1])

    '''
    Find_Pool
        Args:
            - pool_id
        Return:
            pool = Storage.get(pool_id)
            - True ? pool
            - False ? !pool
     '''
    elif operation == 'findPool':
        if len(args) == 1:
            return find_pool(ctx, args[0])

    '''
    Destory_Complete_Pool
        Args:
            - operator_msg
            - address
        Return: 
            - True ? Transfer
            - False ? !Transfer
    '''
    elif operation == 'finishPool':
        if len(args) == 2:
            return finish_pool(ctx, args[0], args[1])

#################
 
    WorkFlow Functions:
    Validate_Op_key:
        Args:
            - operator_key
            - pool_id
        Return:
            pool = Storage.get(pool_id)
            valid = decryptMsg(pool,operator_key)
            - True ? valid
            - False ? !valid
    Validate_Msg:
        Args:
            - pool_id
            - operator_msg
        Return:
            pool = find_pool(pool_id)
            - True ? get_message(pool['operator_key'],operator_msg)
            - False ? !get_message(pool['operator_key'],operator_msg)
    Get_Message:
        Args:
            - operator_key
            - operator_msg
        Return:
            NOTE: decrypt_message is just saying to do a normal check if a message was signed by the same private key
            msg = decrypt_message(operator_key,operator_msg)
            - True ? decrypt_message == valid_operation(msg)
            - False ? !decrypt_message == valid_operation(msg)
    Check_Max_Pool:
        Args:
            - pool_id
            - size
        Return:
            pool = find_pool(pool_id)
            - True ? pool_id['max'] > (pool_id['current'] + size)
            - False ? pool_id['max'] <= (pool_id['current'] + size)
    Add_Deposit_ID:
        Args:
            - deposit_id
            - amount
            - pool_id
        Return:
            - True ? current = storage.get(deposit_ids) && storag.put(current + amount) for pool_id 
            - False ? else
    Workflow:
    Operator Sends following post request:
        {
            "operator_key":"11111", #Public key of wallet
            "operator_msg":"222222", #Sign message of the function they are calling
            "size":"100", #Number of NEO for pool
            "min":"1", #Min number of NEO you can deposit
            "max":"4" #Max number of NEO you can deposit
        }
    System returns, and writes to storage:
        {
            "pool_id":"x000033",
            "size": 100,
            "min": 1,
            "max": 4,
            "current": 0,
            "operator_key":"11111",
            "last_msg":"222222",
            "deposit_ids": {},
            "result":True
        }
    Contributor post following requst:
        {
            "pool_id":"x000033",
            "amount": 1, #Number NEO less than max for pool, need verification call
        }
    System returns:
        {
            "deposit_id":"x9343", #Hash of tx? something more unique?
            "result": True #amount less than max and pool['current'] less than pool['max']
        }
    System executes:
        Take deposit into smart contract
        Verify amount < pool['max'] && pool['size'] for pool_id
        Add deposit_id to pool['deposit_ids'] for correct pool_id
        Increment pool['current'] for pool_id
        Tag Deposited NEO with pool_id
        Invoke smart contract
        Verify pool['operator_key'] == operator_key
        Check that operator_msg && pool['last_msg'] were signed by pool['operator_key']
        Preform operator_msg
        Set pool['last_msg'] == operator_msg
