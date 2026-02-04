#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<vector<char>> table(n, vector<char>(n));
    for (int i = 0; i < n; ++i) {
      for (int j = 0; j < n; ++j) {
        std::cin >> table[i][j];
      }
    }

    vector<int> sum(n);
    for (int i = 0; i < n; ++i) {
      for (int j = 0; j < n; ++j) {
        if (table[i][j] == '1') {
          sum[i]++;
        }
      }
    }

    sort(sum.begin(), sum.end());
    for (int i = 0; i < n; ++i) {
      if (sum[i] == 0) continue;
      if (sum[i] == 1) {
        std::cout << "TRIANGLE"
                  << "\n";
      } else {
        std::cout << "SQUARE"
                  << "\n";
      }

      break;
    }
  }

  return 0;
}
