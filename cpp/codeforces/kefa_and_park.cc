#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int solve(vector<vector<int>>& g, vector<bool>& cats, int m, int cm, int cur_v,
          int from) {
  if (cats[cur_v]) {
    cm--;
  } else {
    cm = m;
  }

  if (cm < 0) return 0;

  if (g[cur_v].size() == 1 && cur_v != 0) return 1;

  int res = 0;
  for (int v : g[cur_v]) {
    if (v == from) continue;
    res += solve(g, cats, m, cm, v, cur_v);
  }

  return res;
}

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;

  vector<bool> cats(n);
  for (int i = 0; i < n; ++i) {
    int buf;
    std::cin >> buf;
    if (buf) {
      cats[i] = true;
    }
  }

  vector<vector<int>> g(n);
  for (int i = 0; i < n - 1; ++i) {
    int x, y;
    std::cin >> x >> y;
    g[x - 1].push_back(y - 1);
    g[y - 1].push_back(x - 1);
  }

  std::cout << solve(g, cats, m, m, 0, 0) << "\n";

  return 0;
}
