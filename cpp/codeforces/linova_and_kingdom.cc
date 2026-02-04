#include <algorithm>
#include <climits>
#include <cmath>
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

typedef vector<vector<int>> graph;

int dfs(graph& g, vector<int64_t>& city_impact, int from, int start,
        int dist_from_capital) {
  int rank = 0;
  for (auto c : g[start]) {
    if (c == from) {
      continue;
    }
    rank += dfs(g, city_impact, start, c, dist_from_capital + 1);
  }

  city_impact[start] = dist_from_capital - rank;
  return rank + 1;
}

int main(int argc, char* argv[]) {
  int n, k;
  std::cin >> n >> k;

  graph g(n);
  for (int i = 0; i < (n - 1); ++i) {
    int a, b;
    std::cin >> a >> b;
    g[a - 1].push_back(b - 1);
    g[b - 1].push_back(a - 1);
  }

  vector<int64_t> city_impact(n);
  dfs(g, city_impact, 0, 0, 0);
  sort(city_impact.begin(), city_impact.end());
  int64_t res = 0;
  for (int i = 0; i < k; ++i) {
    res += city_impact[(n - 1) - i];
  }

  std::cout << res << "\n";

  return 0;
}
