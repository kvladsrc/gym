#include <iostream>
#include <vector>

using std::vector;

int optimal_number(const vector<vector<int>>& g, int n, int m) {
  if (g.size() < n || g.size() < m) return 0;

  vector<bool> excluded(g.size());
  int excluded_num = 0;

  while (g.size() > excluded_num) {
    bool removed = false;

    for (int i = 0; i < g.size(); ++i) {
      if (excluded[i]) continue;

      int connected = 0;
      for (int f = 0; f < g[i].size(); ++f) {
        if (excluded[g[i][f]]) continue;
        connected++;
      }

      if (connected < n ||
          ((static_cast<int>(g.size()) - (excluded_num + 1 + connected)) < m)) {
        excluded[i] = true;
        removed = true;
        excluded_num++;
        break;
      }
    }

    if (!removed) break;
  }

  return g.size() - excluded_num;
}

int main() {
  size_t v;
  size_t e;
  std::cin >> v;
  std::cin >> e;

  vector<vector<int>> g(v);

  for (size_t i = 0; i < e; ++i) {
    int f1;
    int f2;

    std::cin >> f1;
    std::cin >> f2;

    g[f1 - 1].push_back(f2 - 1);
    g[f2 - 1].push_back(f1 - 1);
  }

  int n;
  int m;
  std::cin >> n;
  std::cin >> m;

  std::cout << optimal_number(g, n, m) << std::endl;
}
