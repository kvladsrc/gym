#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

// Asuming the dunning on DAG.
void dfs(vector<vector<int>> &g, int &time, vector<int> &rank, int start) {
  if (rank[start] != 0) {
    return;
  }

  time++;
  for (auto i : g[start]) {
    dfs(g, time, rank, i);
  }
  time++;
  rank[start] = time;
}

vector<int> topological_sort(vector<vector<int>> &g) {
  vector<int> rank(g.size(), 0);
  int time = 0;
  for (size_t i = 0; i < rank.size(); ++i) {
    // Confusing part. Shall we keep results of previous run?
    if (rank[i] == 0) {
      dfs(g, time, rank, i);
    }
  }

  return rank;
}

int main(int argc, char *argv[]) {
  int v, e;
  std::cin >> v >> e;
  vector<vector<int>> g(v);
  while (e--) {
    int a, b;
    std::cin >> a >> b;
    g[a].push_back(b);
  }

  auto table = topological_sort(g);
  for (int i = 0; i < v; ++i) {
    std::cout << i << " " << table[i];
    std::cout << "\n";
  }

  return 0;
}
