#### Function WorkFlow

Pool opperator creates pool in gui passing meta data to the samrt contract. In that meta data is a signed message of some kind by the pool operator with their private key, this grants them access to the outlined parameters of the pool in the meta data that is passed. This will ultimately grant them access to send the amount of NEO passed in the meta data out of the contract provided there is enough NEO sent to the contract with the meta data of the poolID. If there is not it fails, there should not be excess NEO left in the contract. The pools would be creating a promsis that addtional NEO with some given meta data value would be sent to it. At any given time the person that created the pool with some signed message being sent to the smart contract would be able to send the NEO flagged with their poolID to some other NEO address. You could think of this as a smart escrow contract, in that it will escrow NEO for a given wallet and send it when sent the command to do so.


#### Requirements

Usage requires Python 3.6+


#### Installation

Clone the repository and navigate into the project directory. 
Make a Python 3 virtual environment and activate it via

```shell
python3 -m venv venv
source venv/bin/activate
```

or to explicitly install Python 3.6 via

    virtualenv -p /usr/local/bin/python3.6 venv
    source venv/bin/activate

Then install the requirements via

```shell
pip install -r requirements.txt
```

#### Compilation

The template may be compiled as follows

```python
from boa.compiler import Compiler

Compiler.load_and_save('ico_template.py')
```


This will compile your template to `ico_template.avm`



#### Running tests

1. Install `requirements_test.txt`

``` 
pip install -r requirements_test.txt

```

2. Run tests

``` 
python -m unittest discover tests
```

#### Testnet Deployed Details

For testing purposes, this template is deployed on testnet with the following contract script hash:

`0b6c1f919e95fe61c17a7612aebfaf4fda3a2214`

```json
{
    "code": {
        "parameters": "0710",
        "hash": "0b6c1f919e95fe61c17a7612aebfaf4fda3a2214",
        "returntype": 5,
        "script": ".. omitted .."
    },
    "version": 0,
    "code_version": ".2",
    "name": "NEX Ico Template",
    "author": "localhuman",
    "description": "An ICO Template",
    "properties": {
        "dynamic_invoke": false,
        "storage": true
    },
    "email": "tom@neonexchange.org"
}
```

