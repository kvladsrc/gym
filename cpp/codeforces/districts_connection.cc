#include <algorithm>
#include <climits>
#include <cmath>
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
    vector<int> d(n);
    int first_band = -1;
    bool r = false;
    for (auto &i : d) {
      std::cin >> i;
      if (first_band == -1) {
        first_band = i;
      } else if (first_band != i) {
        r = true;
      }
    }

    if (!r) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    std::cout << "YES"
              << "\n";
    // d[0] -- tree root.
    int left_child = -1;
    for (int i = 1; i < n; ++i) {
      if (d[i] != d[0]) {
        if (left_child == -1) {
          left_child = i;
        }

        std::cout << 1 << " " << i + 1 << std::endl;
      }
    }

    for (int i = 1; i < n; ++i) {
      if (d[0] == d[i]) {
        std::cout << left_child + 1 << " " << i + 1 << std::endl;
      }
    }
  }

  return 0;
}
