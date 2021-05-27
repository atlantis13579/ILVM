
#include "stdio.h"
#include <algorithm>
#include <string>
#include <chrono>
#include <vector>

#include "ExecutorLua.h"

void Benchmark()
{
	ExecutorLua exec("test.lua");
	using namespace std::chrono;

	int R;

	steady_clock::time_point t1 = steady_clock::now();
	int Count = 1;
	for (int i = 0; i < Count; ++i)
	{
		R = exec.Execute<int>("Benchmark");
	}
	steady_clock::time_point t2 = steady_clock::now();

	printf("R = %s, TimeSpan = %f ms\n", std::to_string(R).c_str(), std::chrono::duration_cast<std::chrono::milliseconds>(t2 - t1).count() * 1.0f / Count);
}

int main()
{
	printf("Running Lua...\n");
	
	{
		ExecutorLua exec("test.lua");
		auto R = exec.Execute<int>("Add", 1, 2);
		printf("Add(%d, %d) = %s\n", 1, 2, std::to_string(R).c_str());
	}

	Benchmark();

	return 0;
}