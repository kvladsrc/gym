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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n, m;
  std::cin >> n >> m;

  vector<vector<int>> table(n, vector<int>(m));
  for (size_t y = 0; y < n; ++y) {
    for (size_t x = 0; x < m; ++x) {
      std::cin >> table[y][x];
    }
  }

  vector<bool> y_crd(n, false);
  vector<bool> x_crd(m, false);
  bool orig_has_any = false, res_has_any = false;
  for (size_t y = 0; y < n; ++y) {
    for (size_t x = 0; x < m; ++x) {
      if (table[y][x] == 0) {
        y_crd[y] = true;
        x_crd[x] = true;
        continue;
      }

      orig_has_any = true;
    }
  }

  bool wrong = false;
  for (size_t y = 0; y < n; ++y) {
    for (size_t x = 0; x < m; ++x) {
      if (y_crd[y] && x_crd[x] && table[y][x] == 1) {
        wrong = true;
        break;
      }

      if (y_crd[y] || x_crd[x]) {
        table[y][x] = 0;
      }

      if (table[y][x] == 1) {
        res_has_any = true;
      }
    }
    if (wrong) {
      break;
    }
  }

  if (wrong || orig_has_any != res_has_any) {
    std::cout << "NO" << "\n";
  } else {
    std::cout << "YES" << "\n";
    for (size_t y = 0; y < n; ++y) {
      for (size_t x = 0; x < m; ++x) {
        std::cout << table[y][x] << " ";
      }
      std::cout << "\n";
    }
  }

  return 0;
}
