from boa.interop.Neo.Runtime import CheckWitness, Notify
from boa.interop.Neo.Action import RegisterAction
from boa.interop.Neo.Storage import *
from boa.builtins import concat

from fuckstick.token import *


OnTransfer = RegisterAction('transfer', 'addr_from', 'addr_to', 'amount')
OnApprove = RegisterAction('approve', 'addr_from', 'addr_to', 'amount')


def handle_nep51(ctx, operation, args):

    if operation == 'name':
        return TOKEN_NAME

    elif operation == 'decimals':
        return TOKEN_DECIMALS

    elif operation == 'symbol':
        return TOKEN_SYMBOL

    elif operation == 'totalSupply':
        return Get(ctx, TOKEN_CIRC_KEY)

    elif operation == 'balanceOf':
        if len(args) == 1:
            return Get(ctx, args[0])

    elif operation == 'transfer':
        if len(args) == 3:
            return do_transfer(ctx, args[0], args[1], args[2])

    elif operation == 'transferFrom':
        if len(args) == 3:
            return do_transfer_from(ctx, args[0], args[1], args[2])

    elif operation == 'approve':
        if len(args) == 3:
            return do_approve(ctx, args[0], args[1], args[2])

    elif operation == 'allowance':
        if len(args) == 2:
            return do_allowance(ctx, args[0], args[1])

    '''
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
     '''
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

    '''
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
    '''

    return False


def do_transfer(ctx, t_from, t_to, amount):

    if amount <= 0:
        return False

    if len(t_to) != 20:
        return False

    if CheckWitness(t_from):

        if t_from == t_to:
            print("transfer to self!")
            return True

        from_val = Get(ctx, t_from)

        if from_val < amount:
            print("insufficient funds")
            return False

        if from_val == amount:
            Delete(ctx, t_from)

        else:
            difference = from_val - amount
            Put(ctx, t_from, difference)

        to_value = Get(ctx, t_to)

        to_total = to_value + amount

        Put(ctx, t_to, to_total)

        OnTransfer(t_from, t_to, amount)

        return True
    else:
        print("from address is not the tx sender")

    return False


def do_transfer_from(ctx, t_from, t_to, amount):

    if amount <= 0:
        return False

    available_key = concat(t_from, t_to)

    if len(available_key) != 40:
        return False

    available_to_to_addr = Get(ctx, available_key)

    if available_to_to_addr < amount:
        print("Insufficient funds approved")
        return False

    from_balance = Get(ctx, t_from)

    if from_balance < amount:
        print("Insufficient tokens in from balance")
        return False

    to_balance = Get(ctx, t_to)

    new_from_balance = from_balance - amount

    new_to_balance = to_balance + amount

    Put(ctx, t_to, new_to_balance)
    Put(ctx, t_from, new_from_balance)

    print("transfer complete")

    new_allowance = available_to_to_addr - amount

    if new_allowance == 0:
        print("removing all balance")
        Delete(ctx, available_key)
    else:
        print("updating allowance to new allowance")
        Put(ctx, available_key, new_allowance)

    OnTransfer(t_from, t_to, amount)

    return True


def do_approve(ctx, t_owner, t_spender, amount):

    if len(t_spender) != 20:
        return False

    if not CheckWitness(t_owner):
        return False

    if amount < 0:
        return False

    # cannot approve an amount that is
    # currently greater than the from balance
    if Get(ctx, t_owner) >= amount:

        approval_key = concat(t_owner, t_spender)

        if amount == 0:
            Delete(ctx, approval_key)
        else:
            Put(ctx, approval_key, amount)

        OnApprove(t_owner, t_spender, amount)

        return True

    return False


def do_allowance(ctx, t_owner, t_spender):

    return Get(ctx, concat(t_owner, t_spender))
