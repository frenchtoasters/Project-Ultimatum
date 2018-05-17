
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

