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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    string s;
    std::cin >> s;

    int res = 0;
    int a = n;
    int b = n;
    int c = n;
    int d = n;

    for (auto el : s) {
      switch (el) {
        case 'A':
          if (a > 0) {
            res++;
          }
          a--;
          break;
        case 'B':
          if (b > 0) {
            res++;
          }
          b--;
          break;
        case 'C':
          if (c > 0) {
            res++;
          }
          c--;
          break;
        case 'D':
          if (d > 0) {
            res++;
          }
          d--;
          break;
        default:
          continue;
      }
    }
    std::cout << res << "\n";
  }

  return 0;
}
