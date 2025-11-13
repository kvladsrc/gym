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

int reduce(int n) {
  int res = 0;
  while (n != 1) {
    if (n % 3) {
      return -1;
    }

    if (n % 6 == 0) {
      n /= 6;
    } else {
      n *= 2;
    }
    res++;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    std::cout << reduce(n) << "\n";
  }
  return 0;
}
