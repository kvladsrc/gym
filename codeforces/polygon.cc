#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<vector<char>> polygon(n, vector<char>(n));
    for (int row = 0; row < n; ++row) {
      for (int col = 0; col < n; ++col) {
        std::cin >> polygon[row][col];
      }
    }

    bool result = true;
    for (int row = 0; row < n; ++row) {
      for (int col = 0; col < n; ++col) {
        if (polygon[row][col] == '1') {
          bool check_down = true;
          bool check_left = true;
          if (row + 1 != n && polygon[row + 1][col] != '1') {
            check_down = false;
          }
          if (col + 1 != n && polygon[row][col + 1] != '1') {
            check_left = false;
          }

          result = check_down || check_left;
          if (!result) break;
        }
      }

      if (!result) break;
    }

    std::cout << (result ? "YES" : "NO") << "\n";
  }

  return 0;
}
