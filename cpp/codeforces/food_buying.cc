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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int x;
    std::cin >> x;

    int res = 0;
    while (x) {
      if (x < 10) {
        res += x;
        break;
      }
      res += (x / 10) * 10;
      x = x % 10 + x / 10;
    }

    std::cout << res << "\n";
  }

  return 0;
}
