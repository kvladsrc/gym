#include <iostream>
#include <string>

using std::string;

bool is_dangerous(const string& field) {
  char cur = field[0];
  int num = 1;
  for (size_t i = 1; i < field.size(); ++i) {
    if (field[i] == cur) {
      num++;
      if (num >= 7) {
        return true;
      }
      continue;
    } else {
      num = 1;
      cur = field[i];
    }
  }

  return false;
}

int main(int argc, char* argv[]) {
  string field;
  std::cin >> field;

  if (is_dangerous(field))
    std::cout << "YES"
              << "\n";
  else
    std::cout << "NO"
              << "\n";

  return 0;
}
