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
    int n, q;
    std::cin >> n >> q;
    vector<int64_t> summands(n + 1);
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      summands[i] = buf + summands[i - 1];
    }

    while (q--) {
      int l, r, k;
      std::cin >> l >> r >> k;

      int64_t res = summands[l - 1];
      res += k * (r - l) + k;
      res += summands[n] - summands[r];

      if (res % 2) {
        std::cout << "YES"
                  << "\n";
      } else {
        std::cout << "NO"
                  << "\n";
      }
    }
  }

  return 0;
}
