qlik-qms-api
============

command line to get server metadata

Very simple command line to make use of server metadata, especially thinking on powershell.

QvQms usage: QvQms `<options>`

    -h shows usage
    
    tasks
        -ta list all tasks
        -tf <name> find task by name
        -t <id> get task by id

    documents
        -da list all user documents
        -du list all user documents access entries
        -d-add-access <doc> <username|groupname> adds user or group to doc authorization list
        -d-remove-access <doc> <username|groupname> adds user or group to doc authorization list
        
The idea is to increase options as needed
