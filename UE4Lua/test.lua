
function Fact(n)
	if n == 0 then
        return 1
    else
        return n * Fact(n-1)
    end
end

function Add(a, b)
	return a + b
end

function Benchmark()
	s = 0
	for i = 0, 10000000, 1 do
        j = (i % 25163)
        j = (j * j) % 25163
        j = (j * 113) % 25163
        s = (s * 13 + j) % 25163
	end
	return s
end
