#include <algorithm>
#include <iostream>
#include <vector>

using std::max;
using std::vector;

static int optimal_weight(int W, const vector<int>& w) {
  vector<vector<int>> table;
  table.resize(w.size() + 1, vector<int>(W + 1));

  for (int j = 1; j <= static_cast<int>(w.size()); ++j) {
    for (int i = 1; i <= W; ++i) {
      if (w[j - 1] > i) {
        table[j][i] = table[j - 1][i];
      } else {
        table[j][i] =
            std::max(table[j - 1][i], table[j - 1][i - w[j - 1]] + w[j - 1]);
      }
    }
  }

  return table[w.size()][W];
}

int main() {
  int n = 0;
  int W = 0;
  std::cin >> W >> n;
  vector<int> w(n);
  for (int i = 0; i < n; i++) {
    std::cin >> w[i];
  }
  std::cout << optimal_weight(W, w) << '\n';
}
