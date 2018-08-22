sqlcmd -S .\SQLEXPRESS -i "SwapStructureLinks.sql"
sqlcmd -S .\SQLEXPRESS -i "DeleteUnlocatedStructures.sql"
sqlcmd -S .\SQLEXPRESS -i "AssignSynapsesToCells.sql"