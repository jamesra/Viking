-- Fix for DbUpdateConcurrencyException caused by old static concurrency stamps
-- This script updates any users that have the old static concurrency stamp to use a new dynamic one

-- Check if there are any users with the old static concurrency stamp
SELECT 
    Id, 
    UserName, 
    ConcurrencyStamp,
    'Has old static concurrency stamp' as Issue
FROM [AspNetUsers] 
WHERE [ConcurrencyStamp] = '00000000-0000-0000-0000-000000000002'

UNION ALL

SELECT 
    Id, 
    UserName, 
    ConcurrencyStamp,
    'Has null or empty concurrency stamp' as Issue
FROM [AspNetUsers] 
WHERE [ConcurrencyStamp] IS NULL OR [ConcurrencyStamp] = ''

-- Fix the concurrency stamps
UPDATE [AspNetUsers] 
SET [ConcurrencyStamp] = NEWID()
WHERE [ConcurrencyStamp] = '00000000-0000-0000-0000-000000000002' 
   OR [ConcurrencyStamp] IS NULL
   OR [ConcurrencyStamp] = ''

-- Verify the fix
SELECT 
    Id, 
    UserName, 
    ConcurrencyStamp,
    'Fixed - now has dynamic concurrency stamp' as Status
FROM [AspNetUsers] 
WHERE Id IN (
    SELECT Id FROM [AspNetUsers] 
    WHERE [ConcurrencyStamp] != '00000000-0000-0000-0000-000000000002' 
      AND [ConcurrencyStamp] IS NOT NULL 
      AND [ConcurrencyStamp] != ''
)

