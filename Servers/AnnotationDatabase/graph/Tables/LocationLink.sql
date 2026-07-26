CREATE TABLE [graph].[LocationLink] (
    [Username] NVARCHAR (254) NOT NULL,
    [Created]  DATETIME       NOT NULL,
    [Distance] FLOAT (53)     NOT NULL,
    CONSTRAINT [PK_LocationLink] PRIMARY KEY CLUSTERED ([$edge_id] ASC) WITH (FILLFACTOR = 90),
    /*This is commented because the tooling seems to be behind SQL server and the CONNECTION keyword
      is not recognized.  It should be uncommented when VS 2022 comes out in a month or so */
/*    CONSTRAINT EC_LocationLink1 CONNECTION (graph.Location TO graph.Location) */
) AS EDGE;

