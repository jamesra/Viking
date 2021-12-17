
				CREATE FUNCTION dbo.ufnTriangleArea(@P1 geometry, @P2 geometry, @P3 geometry)
				RETURNS float 
				AS 
				-- Returns the stock level for the product.
				BEGIN
					DECLARE @ret float
					DECLARE @S float
					DECLARE @A float
					DECLARE @B float
					DECLARE @C float
					set @A = @P1.STDistance(@P2)
					set @B = @P2.STDistance(@P3)
					set @C = @P3.STDistance(@P1)
					set @S = (@A + @B + @C) / 2.0
					set @ret = SQRT(@S * (@S - @A) * (@S - @B) * (@S - @C))
					RETURN @ret;
				END
				
GO
GRANT EXECUTE
    ON OBJECT::[dbo].[ufnTriangleArea] TO PUBLIC
    AS [dbo];

