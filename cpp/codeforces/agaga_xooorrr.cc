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
    int n;
    std::cin >> n;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    // a a -> a a
    // a a a -> a a a
    // a a a a -> a a
    // a a a a a -> a a a
    // a a a a a a -> a a a a -> a a
    //
    // So, every case can be reduced to two or three
    // elements. Assosiativity can be proved by checking every case of
    // three elements. So we need to check all two or three elements.

    vector<int> acc_right(n);
    int acc = 0;
    for (int i = (n - 1); i >= 0; --i) {
      acc ^= a[i];
      acc_right[i] = acc;
    }

    int first = 0;
    bool res = false;
    for (int i = 0; i < (n - 1); ++i) {
      first ^= a[i];

      int second = 0;
      for (int j = i + 1; j < n; ++j) {
        second ^= a[j];

        if ((j + 1) < n) {
          int third = acc_right[j + 1];

          if (first == second && first == third) {
            res = true;
            goto end_loops;
          }
        }
      }

      if (first == second) {
        res = true;
        goto end_loops;
      }
    }

  end_loops:
    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
