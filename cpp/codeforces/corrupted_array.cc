#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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
using std::sqrt;
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
    n += 2;

    vector<int> b(n);
    int64_t sum = 0;
    for (auto &i : b) {
      std::cin >> i;
      sum += i;
    }

    sort(b.begin(), b.end());

    // Trying to guess i b_{n+2} number. sum - i = maximal number in
    // b;

    int b_n_plus_2_pos = -1;
    for (int i = 0; i < n; ++i) {
      int maximal;

      if (i == (n - 1)) {
        // Last elem.
        maximal = b[n - 2];
      } else {
        maximal = b.back();
      }

      if (((sum - b[i]) - maximal) == maximal) {
        b_n_plus_2_pos = i;
        break;
      }
    }

    if (b_n_plus_2_pos != -1) {
      int i = 0;
      for (int el = 0; el < (n - 2);) {
        if (i != b_n_plus_2_pos) {
          std::cout << b[i] << " ";
          el++;
        }
        i++;
      }

      std::cout << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
