DECLARE @DeleteID bigint
Set @DeleteID = 449

delete from LocationLink
where A in 
(
Select ID from Location 
where ParentID = @DeleteID ) 

delete from LocationLink
where B in 
(
Select ID from Location 
where ParentID = @DeleteID ) 

delete from Location
where ParentID=@DeleteID

delete from Structure
where ID=@DeleteID
