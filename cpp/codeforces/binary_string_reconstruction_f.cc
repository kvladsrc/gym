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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n0, n1, n2;
    std::cin >> n0 >> n1 >> n2;

    string res;

    // n0 = n1 = n2 = 3:
    if (n0 || n1) {
      res.push_back('0');
      while (n0--) {
        res.push_back('0');
      }
    } else {
      res.push_back('1');
    }

    bool post_n2 = n1 % 2 == 0 && n1;
    if (post_n2) {
      n1--;
    }
    bool state_one = true;
    while (n1--) {
      if (state_one) {
        res.push_back('1');
      } else {
        res.push_back('0');
      }
      state_one = !state_one;
    }

    while (n2--) {
      res.push_back('1');
    }
    if (post_n2) {
      res.push_back('0');
    }

    std::cout << res << "\n";
  }

  return 0;
}
