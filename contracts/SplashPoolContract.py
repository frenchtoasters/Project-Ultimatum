from boa.interop.Neo.Runtime import GetTrigger, CheckWitness
from boa.interop.Neo.TriggerType import Application, Verification
from boa.interop.System.ExecutionEngine import GetExecutingScriptHash, GetCallingScriptHash
from boa.interop.Neo.App import RegisterAppCall

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
        if CheckWitness(OWNER):
            return True

        return False

    elif trigger == Application():

        #Create_Pool Function 
        if operation == 'createPool':
            if len(args) != 6:
                return False
            pool_id = Get('pool_id_latest')
            pool_id = pool_id + 1

            #Create pool_id storage Obj
            #Could need more context to know about specific NEO assoicated with it
            if !Put(pool_id+':operator_key',args[0]):
                return False
            if !Put(pool_id+':operator_msg',args[1]:
                return False
            if !Put(pool_id+':size',args[2]):
                return False
            if !Put(pool_id+':min',args[3]):
                return False
            if !Put(pool_id+':max',args[4]):
                return False
            if !Put(pool_id+':state',agrgs[5]):
                return False
            if !Put(pool_id+':deposit_ids',[]):
                return False
            if !Put(pool_id+':current','0'):
                return False
            
            #Increment pool_id_latest
            if !Put('pool_id_latest',pool_id):
                return False

            #Something Failed
            print('staked storage call failed')
            return False

        #Contributor_Deposit Function
        if operation == 'depositPool':
            if len(args) != 3:
                return False
            pool_id = args[0]
            assetID = args[1]
            amount = args[2]
            
            #Validate amount
            if amount < Get(pool_id+':min'):
                return False
            if amount > Get(pool_id+':max'):
                return False
            
            #Validate pool not full
            size = Get(pool_id+':size')
            current_new = amount + size
            if current_new > size:
                return False
            
            #Update Hash of deposit_ids
            reciepts = Get(pool_id+':deposit_ids')
            reciept = genDeposit(args)
            if reciept:
                reciepts = ids.append(reciept)
                if !Put(pool_id+':depsit_ids',reciepts):
                    return False
            return reciept

        #Could also be called operatorWithdraw
        if operation == 'ownerWithdraw':
            if not CheckWitness(OWNER):
                print('only the contract owner can withdraw MCT from the contract')
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


def handle_token_received(chash, args):
    '''
    This section needs a rewrite to handle the switch of token to take anything sent, other than MCT if the MCT balance is < min stake, and then forward it to operator_key(public key of operator)[pool_id]
    Workflow:
        Args:
            from
            to
            amount
            pool_id
        Return:
            - True ? Tx to pool_id == True
            - False ? Tx to pool_id == False || TBD # There is probably something im forgetting
        Script:
            - Verifiy arguments sent are valid, return False if not
            - Verifiy if from is owner, accept MCT <= minStake. If from other accept SPLASH as fee payment, else reject
            - Take deposit and forward to pool_id['operator_key']
    '''
    arglen = len(args)

    if arglen < 3:
        print('arg length incorrect')
        return False

    t_from = args[0]
    t_to = args[1]
    t_amount = args[2]

    if arglen == 4:
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

