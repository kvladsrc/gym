#include <iostream>
#include <string>

using std::string;

int need_to_remove(const string& a) {
  int result = 0;
  char prev = a[0] + 1;
  for (char c : a) {
    if (prev == c) result++;
    prev = c;
  }

  return result;
}

int main(int argc, char* argv[]) {
  string input;
  int i;
  std::cin >> i;
  std::cin >> input;
  std::cout << need_to_remove(input) << "\n";

  return 0;
}
