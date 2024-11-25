#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

bool is_lucky(int n) {
  int left = 0, right = 0;
  int pos = 0;
  while (n) {
    if (pos <= 2) {
      left += n % 10;
    } else {
      right += n % 10;
    }
    n /= 10;
    pos++;
  }
  return left == right;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t) {
    int num;
    std::cin >> num;
    if (is_lucky(num)) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
      ;
    }
    t--;
  }

  return 0;
}
