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
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

const int modulo = 1000000007;
const int mmax = 200000;

void precalc(vector<vector<int>>& table) {
  for (int d = 0; d < 10; ++d) {
    table.front()[d] = 0;
  }
  table.front()[0] = 1;

  for (int i = 1; i <= mmax + 9; ++i) {
    for (int d = 0; d < 9; ++d) {
      table[i][d + 1] = table[i - 1][d];
    }
    table[i][0] = table[i - 1][9];
    table[i][1] = (table[i][1] + table[i - 1][9]) % modulo;
  }
}

int len(vector<int>& digits) {
  int res = 0;
  for (auto d : digits) {
    res = (res + d) % modulo;
  }
  return res;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  vector<vector<int>> table(mmax + 10, vector<int>(10));
  precalc(table);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    int m = 0;
    std::cin >> m;

    // Idea is to precalculate f(d, m) for every digit from 0 to 9,
    // then the answer is sum of f(d, m) for every d in N.

    int res = 0;
    while (n) {
      auto d = n % 10;
      res = (res + len(table[m + d])) % modulo;
      n /= 10;
    }
    std::cout << res << "\n";
  }

  return 0;
}
