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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    // Idea is to count zeroes at j bit for each element. Then apply
    // the operation from left to right bit if it possible to have 0
    // zeroes, skip the bit otherwise.

    vector<int> bits(31, 0);
    while (n--) {
      int buf;
      std::cin >> buf;

      int pos = 30;
      while (pos >= 0) {
        if (buf % 2 == 0) {
          bits[pos]++;
        }
        buf /= 2;
        pos--;
      }
    }

    for (int pos = 0; pos <= 30; ++pos) {
      if (bits[pos] <= k) {
        k -= bits[pos];
        bits[pos] = 0;
      }
    }

    int res = 0;
    for (int pos = 0; pos <= 30; ++pos) {
      res *= 2;
      if (bits[pos] == 0) {
        res++;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
