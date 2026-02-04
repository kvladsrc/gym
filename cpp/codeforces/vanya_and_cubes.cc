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

int need_to_layer(int i) { return (i * i + i) / 2; }

int max_height(int i) {
  // 1 + 2 + 3 + ... + n
  // ((n + 1) * n) / 2
  // (n^2 + n) / 2
  int len = 0;
  while (need_to_layer(len + 1) <= i) {
    i -= need_to_layer(len + 1);
    len++;
  }
  return len;
}

int main(int argc, char* argv[]) {
  int i;
  std::cin >> i;
  std::cout << max_height(i) << "\n";

  return 0;
}
