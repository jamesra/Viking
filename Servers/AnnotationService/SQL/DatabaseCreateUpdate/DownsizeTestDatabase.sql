use [TESTTWO]
delete Location where Radius <= 0.1
delete from LocationLink where A in (select ID from Location where Z < 240 or Z > 260) or B in (select ID from Location where Z < 240 or Z > 260)
delete Location where Z < 240 or Z > 260