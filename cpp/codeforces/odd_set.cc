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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    n *= 2;
    int evens = 0, odds = 0;

    while (n--) {
      int buf;
      std::cin >> buf;
      if (buf % 2) {
        odds++;
      } else {
        evens++;
      }
    }

    if (evens != odds) {
      std::cout << "NO"
                << "\n";
    } else {
      std::cout << "YES"
                << "\n";
    }
  }
  return 0;
}
