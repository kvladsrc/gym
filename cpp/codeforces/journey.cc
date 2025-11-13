#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iomanip>
#include <iostream>
#include <string>
#include <vector>

using std::endl;
using std::string;
using std::vector;

using graph = vector<vector<int>>;

float dfs(graph &g, int start, int p) {
  if (g[start].size() == 1 && start != p) {
    return 0;
  }

  float sum = 0;
  float c = 0;
  for (auto n : g[start]) {
    if (n == p) {
      continue;
    }

    c++;
    sum += dfs(g, n, start);
  }

  if (c == 0) {
    return 0;
  }

  return 1 + sum / c;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n;
  std::cin >> n;

  if (n == 1) {
    std::cout << 0 << "\n";
    return 0;
  }

  graph g(n);

  for (int i = 1; i < n; ++i) {
    int a, b;
    std::cin >> a >> b;
    a--;
    b--;
    g[a].push_back(b);
    g[b].push_back(a);
  }

  auto res = dfs(g, 0, 0);
  std::cout << std::fixed << std::setprecision(15) << res << endl;

  return 0;
}
