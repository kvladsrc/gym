#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    char cur;
    std::cin >> cur;
    n--;

    while (n--) {
      char buf;
      std::cin >> buf;
      if (buf == cur) {
        std::cout << cur;

        if (n) {
          std::cin >> cur;
          n--;
        }
      }
    }
    std::cout << "\n";
  }

  return 0;
}
