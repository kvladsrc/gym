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

    if (n % 3 == 0) {
      int buf = n / 3;
      std::cout << buf << " " << buf + 1 << " " << buf - 1 << "\n";
      continue;
    }

    if (n % 3 == 1) {
      int buf = n / 3;
      std::cout << buf << " " << buf + 2 << " " << buf - 1 << "\n";
      continue;
    }

    if (n % 3 == 2) {
      int buf = n / 3;
      std::cout << buf + 1 << " " << buf + 2 << " " << buf - 1 << "\n";
      continue;
    }
  }

  return 0;
}
