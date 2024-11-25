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
    int t_1, t_2, t_3;
    std::cin >> t_1 >> t_2 >> t_3;

    if (t_1 == t_2 + t_3 || t_2 == t_1 + t_3 || t_3 == t_2 + t_1) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    if (t_1 % 2 == 0 && t_2 == t_3) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    if (t_2 % 2 == 0 && t_1 == t_3) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    if (t_3 % 2 == 0 && t_2 == t_1) {
      std::cout << "YES"
                << "\n";
      continue;
    }

    std::cout << "NO"
              << "\n";
  }

  return 0;
}
