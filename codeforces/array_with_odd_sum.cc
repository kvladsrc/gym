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
    int n, temp_n;
    std::cin >> n;
    temp_n = n;

    bool odd = false, even = false;
    while (temp_n--) {
      int num;
      std::cin >> num;

      if (num % 2) {
        odd = true;
      } else {
        even = true;
      }
    }

    if (!odd) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    if (!even && n % 2 == 0) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    std::cout << "YES"
              << "\n";
  }
  return 0;
}
