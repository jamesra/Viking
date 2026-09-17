How to measure path length:

1. Make the excel file with three columns:
	Code	Start_ID	End_ID

2. Save each excel file sheet as a .csv file:
	a. Make sure you select "Save as Type" and select CSV
	b. Open the csv
		1. Ensure the top row has the headers: Code,Start_ID,End_ID
		2. Remove any rows with text or non-parsable data
	
3. Open SQL Server Studio
	a. There should be a database named "ExternalQueryData"
	b. Right-Click the database to open the context menu.
	c. Select Tasks->Import Flat File
	d. In the wizard, select the csv file you exported and cleaned up from Excel
	e. Continue through the wizard, chech that it detects all three column
	f. Continue through the wizard, mark the "Code" column as primary key (Optional, but prevents duplicate letter codes)
	g. Continue through wizard until hopefully you see "Operation Complete"
	h. Close wizard
	i. If desired, right-click the Tables folder and select "refresh" if you want to see your new table in the "ExternalQueryData" database.
4. Run the path query
	a. Open "PathLengthMeasurement.sql" at this time it lives in Viking\Server\SQL\InDevelopment\PathLengthMeasurement.sql
	b. Look for the line, near the top, that references the ExternalQueryData table:
		DECLARE path_cursor CURSOR FOR
			SELECT Code, Start_ID, End_ID FROM [ExternalQueryData].dbo.RC1_514;
	c. Update the line, replacing RC1_514 with the name of the table you imported.
	d. Ensure you are connected to the correct database.
	e. Run the query.
	f. Hopefully results from in the results window.
		1. Results with a 0 in NumSteps had no path from source to target
		2. Results with a -1 in NumSteps had an error calculating the path to the target
		3. Distance should be in nm
		
		
	