#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
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
    int small = 0, big = 0;
    while (n--) {
      int buf;
      std::cin >> buf;
      if (buf == 1)
        small++;
      else
        big++;
    }
    if (small % 2) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    if (big % 2 && small < 2) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    std::cout << "YES"
              << "\n";
  }

  return 0;
}
