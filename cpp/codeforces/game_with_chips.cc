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
#include <utility>
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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  int m = 0;
  int k = 0;
  std::cin >> n >> m >> k;

  vector<pair<int, int>> chips(k);
  for (auto& p : chips) {
    std::cin >> p.first >> p.second;
  }

  vector<pair<int, int>> positions(k);
  for (auto& p : positions) {
    std::cin >> p.first >> p.second;
  }

  /*
   * Lets move all the chips to 1.1 position by using n L operations
   * and m U operations. Then we need n * m operations to visit all cells.
   */

  int moves = n - 1 + m - 1;
  moves += n * m - 1;
  std::cout << moves << "\n";

  for (int i = 1; i < n; ++i) {
    std::cout << "U";
  }

  for (int i = 1; i < m; ++i) {
    std::cout << "L";
  }

  bool to_right = true;
  for (int j = 1; j <= n; ++j) {
    for (int i = 1; i < m; ++i) {
      if (to_right) {
        std::cout << "R";
      } else {
        std::cout << "L";
      }
    }

    to_right = !to_right;

    if (j < n) {
      std::cout << "D";
    }
  }

  return 0;
}
