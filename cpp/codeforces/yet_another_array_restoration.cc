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
    int n, x, y;
    std::cin >> n >> x >> y;

    int delta = y - x;

    int maximal = INT_MAX, period = 0;
    for (int i = 1; i <= delta; ++i) {
      if (delta % i != 0) {
        continue;
      }

      int elems = delta / i + 1;
      if (elems > n) {
        continue;
      }

      int minimal = x, local_maximal = y;
      while (elems < n) {
        if (minimal - i >= 1) {
          minimal -= i;
        } else {
          local_maximal += i;
        }

        elems++;
      }

      if (maximal > local_maximal) {
        maximal = local_maximal;
        period = i;
      }
    }

    for (int i = n - 1; i >= 0; --i) {
      std::cout << maximal - period * i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
