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
    int n;
    std::cin >> n;

    int sum = 0;
    bool mod_one = false;
    bool mod_two = false;
    while (n--) {
      int buf;
      std::cin >> buf;
      sum += buf;

      if (!mod_one && buf % 3 == 1) mod_one = true;
      if (!mod_two && buf % 3 == 2) mod_two = true;
    }

    switch (sum % 3) {
      case 0:
        std::cout << 0 << "\n";
        break;
      case 1:
        if (mod_one) {
          std::cout << 1 << "\n";
        } else {
          std::cout << 2 << "\n";
        }
        break;
      default:
        std::cout << 1 << "\n";
    }
  }

  return 0;
}
