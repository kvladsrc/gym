#include <algorithm>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int a, b;
  std::cin >> a >> b;
  int fashionalbe = min(a, b);
  int rest = (max(a, b) - fashionalbe) / 2;
  std::cout << fashionalbe << " " << rest << "\n";

  return 0;
}
