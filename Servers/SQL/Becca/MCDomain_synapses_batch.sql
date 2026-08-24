/*
 * PURPOSE
 * -------
 * Drive SelectMCDomainSynapses for every glial ID in @GlialIDs.
 * Each EXEC is independent — table variables are not shared across cells.
 *
 * This script only streams result sets to the client (SSMS grids).
 * Use Invoke-MCDomainSynapses.ps1 to write
 *   {GlialID}_Candidates.csv
 *   {GlialID}_SynapseCount.csv
 *   {GlialID}_ClosestPairs.csv
 *   {GlialID}_ShapeMap.csv
 *
 * Deploy SelectMCDomainSynapses.sql once before running this file.
 */

declare @SearchDistance float = 500;

declare @GlialIDs dbo.integer_list;
insert into @GlialIDs (ID) values (8887), (9025);

declare @SynapseTypeIDs dbo.integer_list;
insert into @SynapseTypeIDs (ID) values (73), (34), (28);

declare @GlialID bigint;

declare glial_cursor cursor local fast_forward for
    select ID from @GlialIDs order by ID;

open glial_cursor;
fetch next from glial_cursor into @GlialID;

while @@FETCH_STATUS = 0
begin
    exec dbo.SelectMCDomainSynapses
        @GlialID = @GlialID,
        @SearchDistance = @SearchDistance,
        @SynapseTypeIDs = @SynapseTypeIDs;

    fetch next from glial_cursor into @GlialID;
end

close glial_cursor;
deallocate glial_cursor;
